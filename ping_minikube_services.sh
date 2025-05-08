#!/bin/bash

echo "Minikube Service Ping Script"
echo "============================"
echo ""

echo "Attempting to get Minikube IP..."
MINIKUBE_IP=$(minikube ip)
if [ -z "$MINIKUBE_IP" ]; then
    echo "ERROR: Failed to get Minikube IP. Please ensure Minikube is running and accessible."
    exit 1
fi
echo "Minikube IP: $MINIKUBE_IP"
echo ""

# Function to check a NodePort service with an HTTP health endpoint
check_service() {
    local service_name=$1
    local health_path=$2
    local service_display_name=${3:-$service_name} # Optional display name

    echo "--- Checking $service_display_name ---"
    
    RAW_URLS=$(minikube service "$service_name" --url -n default 2>/dev/null)

    if [ -z "$RAW_URLS" ]; then
        echo "INFO: Could not automatically get URL for $service_display_name using 'minikube service --url'."
        echo "This can happen if the service is not yet fully up, not of type NodePort, or an issue with minikube's service resolution."
        echo "You can manually check with: kubectl get svc $service_name -n default"
        echo "And construct the URL: http://$MINIKUBE_IP:<NodePort>$health_path"
        echo ""
        return
    fi

    found_healthy_endpoint=false
    for SERVICE_URL_BASE in $RAW_URLS; do
        # Ensure SERVICE_URL_BASE is not empty or just whitespace
        if [[ -z "${SERVICE_URL_BASE// }" ]]; then
            continue
        fi

        # Normalize URL: remove trailing slash from base, then add health_path
        CLEAN_SERVICE_URL_BASE=${SERVICE_URL_BASE%/}
        FULL_URL="${CLEAN_SERVICE_URL_BASE}${health_path}"
        
        echo "Attempting to ping: $FULL_URL"
        # -L to follow redirects, -s for silent, -f to fail fast on server errors (HTTP 4xx/5xx)
        if curl -Lsf "$FULL_URL" > /dev/null; then
            echo "SUCCESS: $service_display_name is healthy at $FULL_URL"
            found_healthy_endpoint=true
            break # Found a working endpoint for this service
        else
            echo "FAILURE: Could not reach $service_display_name at $FULL_URL (or it's unhealthy/returned an error)"
        fi
    done

    if ! $found_healthy_endpoint; then
         echo "INFO: Could not find a healthy endpoint for $service_display_name from automatically determined URLs: $RAW_URLS"
         echo "Please double-check the service status ('kubectl get svc $service_name -n default'), its NodePort, and the health path ('$health_path')."
    fi
    echo ""
}

# === NodePort Services ===
echo "*** Checking NodePort Services ***"
check_service "prometheus" "/-/healthy" "Prometheus"
check_service "grafana" "/api/health" "Grafana"
check_service "user-account-service" "/health" "User Account Service (API at :8002)"
check_service "transaction-service" "/api/health" "Transaction Service (API at :8003)"
check_service "frontend" "/" "Frontend (UI at :3001)"
check_service "fraud-detection-service" "/health" "Fraud Detection Service (API at :8000)"
check_service "query-service" "/health" "Query Service (API at :8004)"

# RabbitMQ (NodePort, Management API)
echo "--- Checking RabbitMQ (Management API) ---"
RABBITMQ_NODE_PORT=$(kubectl get svc rabbitmq -n default -o jsonpath='{.spec.ports[?(@.name=="15672")].nodePort}' 2>/dev/null)
RABBITMQ_HEALTHY=false
if [ -n "$RABBITMQ_NODE_PORT" ] && [[ "$RABBITMQ_NODE_PORT" =~ ^[0-9]+$ ]]; then # Check if it's a number
    RABBITMQ_HEALTH_URL="http://${MINIKUBE_IP}:${RABBITMQ_NODE_PORT}/api/aliveness-test/%2F"
    echo "Attempting to ping RabbitMQ Management via NodePort $RABBITMQ_NODE_PORT: $RABBITMQ_HEALTH_URL"
    # Default credentials for RabbitMQ are guest:guest (from your secrets.yaml)
    if curl -Lsf -u guest:guest "$RABBITMQ_HEALTH_URL" > /dev/null; then
        echo "SUCCESS: RabbitMQ Management API is healthy at $RABBITMQ_HEALTH_URL"
        RABBITMQ_HEALTHY=true
    else
        echo "FAILURE: Could not reach RabbitMQ Management API at $RABBITMQ_HEALTH_URL (or it's unhealthy/credentials wrong)."
    fi
else
    echo "INFO: Could not get specific NodePort for RabbitMQ management (service port 15672)."
    echo "Falling back to 'minikube service rabbitmq --url' and trying all returned URLs."
    RAW_RABBITMQ_URLS=$(minikube service rabbitmq --url -n default 2>/dev/null)
    if [ -n "$RAW_RABBITMQ_URLS" ]; then
        for R_URL_BASE in $RAW_RABBITMQ_URLS; do
            if [[ -z "${R_URL_BASE// }" ]]; then continue; fi
            CLEAN_R_URL_BASE=${R_URL_BASE%/}
            RABBITMQ_HEALTH_URL_TRY="${CLEAN_R_URL_BASE}/api/aliveness-test/%2F"
            echo "Trying potential RabbitMQ Management URL: $RABBITMQ_HEALTH_URL_TRY"
            if curl -Lsf -u guest:guest "$RABBITMQ_HEALTH_URL_TRY" > /dev/null; then
                echo "SUCCESS: RabbitMQ Management API is healthy at $RABBITMQ_HEALTH_URL_TRY"
                RABBITMQ_HEALTHY=true
                break
            fi
        done
    fi
fi

if ! $RABBITMQ_HEALTHY; then
    echo "INFO: RabbitMQ Management check failed or could not determine management URL automatically."
    echo "Ensure RabbitMQ is running and its management plugin (service port 15672) is exposed via NodePort."
    echo "You can also test by port-forwarding: kubectl port-forward svc/rabbitmq 15672:15672 -n default"
    echo "Then access http://localhost:15672/api/aliveness-test/%2F in your browser (user: guest, pass: guest)."
fi
echo ""

# === ClusterIP Services ===
echo "*** Guidance for ClusterIP Services (require port-forward or exec) ***"
echo "These services are not directly accessible from outside Minikube without port-forwarding."
echo ""

echo "Elasticsearch (ClusterIP - Service Port 9200):"
echo "To test from your machine:"
echo "1. Run in a separate terminal: kubectl port-forward svc/elasticsearch 9200:9200 -n default"
echo "2. Then, in another terminal: curl http://localhost:9200/_cluster/health?pretty"
echo "To test from within the cluster (example):"
echo "   ES_POD_NAME=\$(kubectl get pods -n default -l app=elasticsearch -o jsonpath='{.items[0].metadata.name}')"
echo "   kubectl exec -n default \"\$ES_POD_NAME\" -- curl -sf http://localhost:9200/_cluster/health"
echo ""

echo "Redis (ClusterIP - Service Port 6379):"
echo "To test from your machine:"
echo "1. Run in a separate terminal: kubectl port-forward svc/redis 6379:6379 -n default"
echo "2. Then, in another terminal: redis-cli -p 6379 ping (Requires redis-cli installed locally)"
echo "To test from within the cluster (example):"
echo "   REDIS_POD_NAME=\$(kubectl get pods -n default -l app=redis -o jsonpath='{.items[0].metadata.name}')"
echo "   kubectl exec -n default \"\$REDIS_POD_NAME\" -- redis-cli ping"
echo ""

echo "MySQL User Account (ClusterIP - Service Port 3307 maps to Pod Port 3306):"
echo "To test from your machine:"
echo "1. Run in a separate terminal: kubectl port-forward svc/mysql-useraccount 3307:3306 -n default"
echo "2. Then, use a MySQL client to connect to localhost:3307."
echo "   Username: root, Password: Get 'MYSQL_PASSWORD_UA' from 'banking-app-secret'."
echo "   Example to get password: kubectl get secret banking-app-secret -n default -o jsonpath='{.data.MYSQL_PASSWORD_UA}' | base64 --decode"
echo "To test from within the cluster (example for ping):"
echo "   MYSQL_UA_POD_NAME=\$(kubectl get pods -n default -l app=mysql-useraccount -o jsonpath='{.items[0].metadata.name}')"
echo "   echo \"Retrieve MYSQL_PASSWORD_UA from banking-app-secret first, then run:\""
echo "   echo \"kubectl exec -n default \\\"\$MYSQL_UA_POD_NAME\\\" -- mysqladmin ping -h127.0.0.1 -uroot -pYOUR_UA_PASSWORD\""
echo ""

echo "MySQL Transaction (ClusterIP - Service Port 3308 maps to Pod Port 3306):"
echo "To test from your machine:"
echo "1. Run in a separate terminal: kubectl port-forward svc/mysql-transaction 3308:3306 -n default"
echo "2. Then, use a MySQL client to connect to localhost:3308."
echo "   Username: root, Password: Get 'MYSQL_PASSWORD_TA' from 'banking-app-secret'."
echo "   Example to get password: kubectl get secret banking-app-secret -n default -o jsonpath='{.data.MYSQL_PASSWORD_TA}' | base64 --decode"
echo "To test from within the cluster (example for ping):"
echo "   MYSQL_TA_POD_NAME=\$(kubectl get pods -n default -l app=mysql-transaction -o jsonpath='{.items[0].metadata.name}')"
echo "   echo \"Retrieve MYSQL_PASSWORD_TA from banking-app-secret first, then run:\""
echo "   echo \"kubectl exec -n default \\\"\$MYSQL_TA_POD_NAME\\\" -- mysqladmin ping -h127.0.0.1 -uroot -pYOUR_TA_PASSWORD\""
echo ""

echo "Script finished."
echo "Note: For 'kubectl exec' commands requiring passwords, you'll need to retrieve them from the 'banking-app-secret' first."