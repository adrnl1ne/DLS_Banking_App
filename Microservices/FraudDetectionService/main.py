import json
import logging
import pika
import time
import random
from fastapi import FastAPI
import uvicorn
from prometheus_client import Counter, start_http_server
import threading
import redis
from datetime import datetime

# Set up console logging for operational information
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s - %(levelname)s - %(message)s",
)

# Set up a separate file logger for errors only
error_logger = logging.getLogger('error_logger')
error_logger.setLevel(logging.ERROR)
file_handler = logging.FileHandler("fraud.log")
file_handler.setLevel(logging.ERROR)
formatter = logging.Formatter("%(asctime)s - %(levelname)s - %(message)s")
file_handler.setFormatter(formatter)
error_logger.addHandler(file_handler)
error_logger.propagate = False

app = FastAPI(title="Fraud Detection Service")

# Prometheus metrics
messages_processed = Counter("messages_processed", "Total number of messages processed")
fraud_detections = Counter("fraud_detections", "Number of fraud cases detected")
processing_errors = Counter("processing_errors", "Number of errors during message processing")
duplicate_messages = Counter("duplicate_messages", "Number of duplicate messages skipped")
duplicate_fraud_checks = Counter("duplicate_fraud_checks", "Number of duplicate fraud check requests")

# Redis client for idempotence, transaction storage, and result storage
redis_client = redis.Redis(host="redis", port=6379, db=0, decode_responses=True)

# RabbitMQ connection setup with retry logic
def get_rabbitmq_connection(max_retries=30, initial_delay=1, max_delay=30):
    """Establish connection to RabbitMQ with exponential backoff retry logic"""
    retry_count = 0
    delay = initial_delay
    
    while retry_count < max_retries:
        try:
            logging.info(f"Attempting to connect to RabbitMQ (attempt {retry_count+1}/{max_retries})")
            connection = pika.BlockingConnection(pika.ConnectionParameters(
                host="rabbitmq",
                connection_attempts=3,
                retry_delay=5
            ))
            logging.info("Successfully connected to RabbitMQ")
            return connection
        except Exception as e:
            retry_count += 1
            if retry_count >= max_retries:
                error_logger.error(f"Failed to connect to RabbitMQ after {max_retries} attempts: {str(e)}")
                raise
            
            # Exponential backoff with jitter
            jitter = random.uniform(0, 0.5) * delay
            sleep_time = min(delay + jitter, max_delay)
            logging.warning(f"RabbitMQ connection failed, retrying in {sleep_time:.2f} seconds: {str(e)}")
            time.sleep(sleep_time)
            delay = min(delay * 2, max_delay)

# Global channel and connection objects
rabbitmq_connection = None
rabbitmq_channel = None

def get_channel():
    """Get or create a channel for publishing"""
    global rabbitmq_connection, rabbitmq_channel
    
    if rabbitmq_connection is None or not rabbitmq_connection.is_open:
        rabbitmq_connection = get_rabbitmq_connection()
        rabbitmq_channel = rabbitmq_connection.channel()
        
        # Declare all queues we'll be using - create them if they don't exist
        try:
            # Use CONSISTENT durable settings - all queues should be durable for reliability
            rabbitmq_channel.queue_declare(queue="FraudResult", durable=True)  # Changed to durable=True
            logging.info("Declared FraudResult queue as durable=True")
            
            rabbitmq_channel.queue_declare(queue="FraudEvents", durable=True)
            logging.info("Declared FraudEvents queue as durable=True")
                
            rabbitmq_channel.queue_declare(queue="FraudCheckResults", durable=True)
            logging.info("Declared FraudCheckResults queue as durable=True")
            
        except Exception as e:
            logging.error(f"Error declaring queues: {e}")
            # If there's a queue mismatch, close the channel and recreate it
            try:
                rabbitmq_channel.close()
                rabbitmq_channel = rabbitmq_connection.channel()
                logging.info("Recreated channel after queue declaration error")
            except Exception as close_ex:
                logging.error(f"Error recreating channel: {close_ex}")
        
    return rabbitmq_channel

def callback(ch, method, properties, body):
    try:
        # Parse the incoming message
        message = json.loads(body)
        transfer_id = message["transferId"]
        amount = message["amount"]

        # DEBUG: Log the entire incoming message to see what we're getting
        logging.info(f"🔍 PROCESSING FRAUD CHECK: TransferID={transfer_id}, Amount=${amount}")
        logging.info(f"📨 FULL MESSAGE: {json.dumps(message, indent=2)}")

        # Check for idempotence using Redis
        if redis_client.get(f"fraud:transfer:{transfer_id}"):
            logging.info(f"⚠️ Duplicate message for transfer {transfer_id}, skipping processing")
            duplicate_messages.inc()
            duplicate_fraud_checks.inc()
            ch.basic_ack(delivery_tag=method.delivery_tag)
            return

        # Mark the transfer as processed (for idempotence)
        redis_client.setex(f"fraud:transfer:{transfer_id}", 604800, "processed")  # 7-day expiry

        # Store transaction details in Redis (for auditing)
        transaction_data = {
            "transferId": transfer_id,
            "amount": amount,
            "timestamp": time.time()
        }
        redis_client.setex(f"fraud:transaction:{transfer_id}", 604800, json.dumps(transaction_data))  # 7-day expiry

        # Fraud detection logic: flag if amount > $1000
        is_fraud = amount > 1000
        status = "declined" if is_fraud else "approved"
        result = {
            "transferId": transfer_id,
            "isFraud": is_fraud,
            "status": status,
            "amount": amount,
            "timestamp": time.strftime("%Y-%m-%dT%H:%M:%S.%fZ", time.gmtime())
        }

        # Update transaction data with fraud detection result
        transaction_data["isFraud"] = is_fraud
        transaction_data["status"] = status
        redis_client.setex(f"fraud:transaction:{transfer_id}", 604800, json.dumps(transaction_data))

        # Store the fraud check result in Redis
        redis_client.setex(f"fraud:result:{transfer_id}", 3600, json.dumps(result))  # 1-hour expiry

        # Increment Prometheus metrics
        messages_processed.inc()
        if is_fraud:
            fraud_detections.inc()

        # Log the result to console only
        logging.info(f"🔍 FRAUD CHECK RESULT: Transfer {transfer_id}, Amount=${amount}, Fraud={is_fraud}, Status={status}")

        # Use the shared channel to publish results
        channel = get_channel()
        
        # Check if this is a delayed check that needs special handling
        is_delayed = message.get("isDelayed", False)
        
        logging.info(f"📤 PUBLISHING fraud result for {transfer_id}, isDelayed={is_delayed}")
        
        # Always publish to QueryService via FraudEvents queue (for monitoring/reporting)
        try:
            channel.basic_publish(
                exchange="",
                routing_key="FraudEvents",
                body=json.dumps({
                    "event_type": "FraudCheckCompleted",
                    "transferId": transfer_id,
                    "isFraud": is_fraud,
                    "status": status,
                    "amount": amount,
                    "timestamp": result["timestamp"]
                }),
                properties=pika.BasicProperties(delivery_mode=2)  # Make message persistent since queue is durable
            )
            logging.info(f"Published FraudCheckCompleted event to FraudEvents queue for {transfer_id}")
        except Exception as e:
            logging.error(f"Failed to publish to FraudEvents queue: {e}")

        # For ALL fraud checks (delayed or immediate), publish to FraudResult queue
        try:
            # Make sure we have a valid channel before publishing
            channel = get_channel()
            
            # Ensure the queue exists before publishing
            try:
                channel.queue_declare(queue="FraudResult", durable=True)  # Changed to durable=True
                logging.info(f"✅ Ensured FraudResult queue exists for {transfer_id}")
            except Exception as queue_ex:
                logging.warning(f"⚠️ Could not declare FraudResult queue: {queue_ex}, will try to publish anyway")
            
            # Use the correct format expected by TransactionService
            fraud_result_message = {
                "transferId": transfer_id,  # Use camelCase
                "isFraud": is_fraud,
                "status": status,
                "amount": amount,
                "timestamp": result["timestamp"]
            }
            
            channel.basic_publish(
                exchange="",
                routing_key="FraudResult",
                body=json.dumps(fraud_result_message),
                properties=pika.BasicProperties(delivery_mode=2)  # Make message persistent since queue is durable
            )
            logging.info(f"✅ PUBLISHED fraud result for {transfer_id} to FraudResult queue: {fraud_result_message}")
        except Exception as e:
            logging.error(f"❌ FAILED to publish fraud result for {transfer_id}: {e}")

        # If this was a delayed check, ALWAYS publish to FraudCheckResults queue
        if is_delayed:
            delayed_result_message = {
                "event_type": "DelayedFraudCheckCompleted",
                "transferId": transfer_id,
                "isFraud": is_fraud,
                "status": status,
                "amount": amount,
                "timestamp": result["timestamp"]
            }
            
            try:
                # Make sure we have a valid channel before publishing
                channel = get_channel()
                
                # Ensure the queue exists before publishing
                try:
                    channel.queue_declare(queue="FraudCheckResults", durable=True)
                    logging.info(f"Ensured FraudCheckResults queue exists for {transfer_id}")
                except Exception as queue_ex:
                    logging.warning(f"Could not declare FraudCheckResults queue: {queue_ex}, will try to publish anyway")
                
                channel.basic_publish(
                    exchange="",
                    routing_key="FraudCheckResults",
                    body=json.dumps(delayed_result_message),
                    properties=pika.BasicProperties(delivery_mode=2)
                )
                logging.info(f"SUCCESS: Published delayed fraud check result for {transfer_id} to FraudCheckResults queue. Message: {delayed_result_message}")
            except Exception as e:
                logging.error(f"FAILED to publish delayed fraud check result for {transfer_id}: {e}")
        else:
            # For immediate checks, publish to FraudResult queue (TransactionService listens here)
            try:
                # Make sure we have a valid channel before publishing
                channel = get_channel()
                
                # Ensure the queue exists before publishing
                try:
                    channel.queue_declare(queue="FraudResult", durable=True)  # Changed to durable=True
                    logging.info(f"Ensured FraudResult queue exists for {transfer_id}")
                except Exception as queue_ex:
                    logging.warning(f"Could not declare FraudResult queue: {queue_ex}, will try to publish anyway")
                
                # Use the correct format expected by TransactionService
                fraud_result_message = {
                    "transferId": transfer_id,  # Use camelCase
                    "isFraud": is_fraud,
                    "status": status,
                    "amount": amount,
                    "timestamp": result["timestamp"]
                }
                
                channel.basic_publish(
                    exchange="",
                    routing_key="FraudResult",
                    body=json.dumps(fraud_result_message),
                    properties=pika.BasicProperties(delivery_mode=2)  # Make message persistent since queue is durable
                )
                logging.info(f"Published immediate fraud check result for {transfer_id} to FraudResult queue: {fraud_result_message}")
            except Exception as e:
                logging.error(f"Failed to publish immediate fraud check result for {transfer_id}: {e}")

        # Acknowledge the message
        ch.basic_ack(delivery_tag=method.delivery_tag)
        logging.info(f"✅ ACKNOWLEDGED fraud check message for {transfer_id}")

    except Exception as e:
        processing_errors.inc()
        error_logger.error(f"💥 Error processing message: {str(e)}")
        logging.error(f"💥 Error processing message: {str(e)}")
        ch.basic_nack(delivery_tag=method.delivery_tag, requeue=False)

# Update the fraud result format with the missing required fields:
def publish_fraud_result(channel, transfer_id, amount, is_fraud=False):
    result = {
        "TransferId": transfer_id,  # Capitalization matters for C# deserialization
        "IsFraud": is_fraud,
        "Status": "declined" if is_fraud else "approved",
        "Amount": float(amount),
        "Timestamp": datetime.utcnow().isoformat()  # Include required timestamp
    }
    
    channel.basic_publish(
        exchange='',
        routing_key='FraudResult',
        body=json.dumps(result)
    )

# Start consuming messages from RabbitMQ
def start_consuming():
    while True:
        try:
            connection = get_rabbitmq_connection()
            channel = connection.channel()
            
            # Make sure the queue exists
            channel.queue_declare(queue="CheckFraud", durable=True)
            logging.info("Declared CheckFraud queue as durable=True")
            
            # Set prefetch count to 1 for fair dispatch
            channel.basic_qos(prefetch_count=1)
            
            # Start consuming
            channel.basic_consume(queue="CheckFraud", on_message_callback=callback, auto_ack=False)
            
            logging.info("Fraud Detection Service started. Waiting for messages...")
            print("Fraud Detection Service started. Waiting for messages...")
            channel.start_consuming()
        except Exception as e:
            error_logger.error(f"Consumer connection error: {str(e)}")
            logging.error(f"Consumer connection error: {str(e)}")
            logging.info("Will try to reconnect in 5 seconds...")
            time.sleep(5)

# FastAPI health check endpoint
@app.get("/health")
async def health_check():
    try:
        # Attempt to connect to RabbitMQ as a health check
        connection = get_rabbitmq_connection(max_retries=1, initial_delay=1)
        connection.close()
        # Attempt to connect to Redis as a health check
        redis_client.ping()
        return {"status": "healthy", "rabbitmq": "connected", "redis": "connected"}
    except Exception as e:
        error_logger.error(f"Health check failed: {str(e)}")
        return {"status": "unhealthy", "rabbitmq": "disconnected", "redis": "disconnected", "error": str(e)}

# Start the RabbitMQ consumer in a separate thread when FastAPI starts
@app.on_event("startup")
async def startup_event():
    # Start Prometheus metrics server
    start_http_server(8001)
    logging.info("Prometheus metrics server started on port 8001")
    
    # Start RabbitMQ consumer in a separate thread
    consumer_thread = threading.Thread(target=start_consuming, daemon=True)
    consumer_thread.start()
    logging.info("RabbitMQ consumer started in background thread")

if __name__ == "__main__":
    # This block will only execute when running the file directly, not with uvicorn
    start_http_server(8001)
    logging.info("Prometheus metrics server started on port 8001")
    
    threading.Thread(target=lambda: uvicorn.run(app, host="0.0.0.0", port=8000), daemon=True).start()
    logging.info("FastAPI server started on port 8000")
    
    start_consuming()