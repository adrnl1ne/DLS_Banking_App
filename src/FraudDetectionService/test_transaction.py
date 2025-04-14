import pika
import json
import random
import time
import uuid
from concurrent.futures import ThreadPoolExecutor

# Publish test messages to the CheckFraud queue
def send_test_messages(num_tests=100, duplicate_id=None):
    connection = pika.BlockingConnection(pika.ConnectionParameters(host="rabbitmq"))
    channel = connection.channel()
    channel.queue_declare(queue="CheckFraud")
    
    results = []
    
    print(f"Sending {num_tests} test messages...")
    for i in range(num_tests):
        # Use a fixed transfer_id for duplicates, or generate a new one
        if duplicate_id and i % 2 == 1:  # Send duplicate every other message
            transfer_id = duplicate_id
        else:
            transfer_id = str(uuid.uuid4())[:8]  # Use first 8 chars of UUID for readability
        
        amount = random.randint(100, 5000)  # Random amount between $100 and $5000
        
        # Create and send test message
        message = {"transferId": transfer_id, "amount": amount}
        channel.basic_publish(
            exchange="", routing_key="CheckFraud", body=json.dumps(message)
        )
        
        print(f"Test {i+1}/{num_tests}: Sent message with ID: {transfer_id}, Amount: ${amount}")
        results.append({"id": transfer_id, "amount": amount})
        
        # Short delay to avoid overwhelming the system
        time.sleep(0.1)
    
    connection.close()
    print(f"All {num_tests} test messages sent!")
    return results

# Consume the results from the FraudResult queue
def consume_results(expected_count=5, timeout=30):
    connection = pika.BlockingConnection(pika.ConnectionParameters(host="rabbitmq"))
    channel = connection.channel()
    channel.queue_declare(queue="FraudResult")
    
    results = []
    start_time = time.time()
    
    def callback(ch, method, properties, body):
        result = json.loads(body)
        is_fraud = result.get("isFraud", False)
        status = result.get("status", "unknown")
        transfer_id = result.get("transferId", "unknown")
        
        results.append(result)
        fraud_status = "🔴 FRAUD" if is_fraud else "🟢 LEGITIMATE"
        print(f"Received result {len(results)}/{expected_count}: ID: {transfer_id}, Status: {status} ({fraud_status})")
        
        # Acknowledge the message
        ch.basic_ack(delivery_tag=method.delivery_tag)
        
        # Stop consuming if we've received all expected messages or timed out
        if len(results) >= expected_count or (time.time() - start_time > timeout):
            channel.stop_consuming()

    channel.basic_consume(queue="FraudResult", on_message_callback=callback)
    
    print(f"Waiting for {expected_count} results from FraudResult queue (timeout: {timeout}s)...")
    
    # Start consuming but set a timeout
    def consume_with_timeout():
        try:
            channel.start_consuming()
        except Exception as e:
            print(f"Error while consuming: {e}")
    
    # Use a thread with timeout to handle the consumption
    with ThreadPoolExecutor(max_workers=1) as executor:
        future = executor.submit(consume_with_timeout)
        try:
            future.result(timeout=timeout)
        except TimeoutError:
            print(f"Timeout reached after {timeout} seconds")
            channel.stop_consuming()
    
    connection.close()
    
    # Print summary
    total_received = len(results)
    fraud_count = sum(1 for r in results if r.get("isFraud", False))
    legitimate_count = total_received - fraud_count
    
    print("\n===== RESULTS SUMMARY =====")
    print(f"Total messages received: {total_received}/{expected_count}")
    print(f"Fraud transactions: {fraud_count} ({fraud_count/total_received*100:.1f}%)")
    print(f"Legitimate transactions: {legitimate_count} ({legitimate_count/total_received*100:.1f}%)")
    print("==========================\n")
    
    return results

if __name__ == "__main__":
    num_tests = 100
    # Use a fixed transfer_id for duplicates
    duplicate_id = "test-duplicate-id"
    
    print(f"🚀 Starting test with {num_tests} transactions, some duplicates")
    sent_transactions = send_test_messages(num_tests, duplicate_id=duplicate_id)
    time.sleep(1)  # Give some time for processing to begin
    received_results = consume_results(expected_count=num_tests - (num_tests // 2))  # Expect fewer results due to duplicates