#!/bin/bash
# Service Monitoring Script for MonitraNG
# Monitors all services and sends alerts if any service is down

set -e

# Configuration
ALERT_EMAIL="${ALERT_EMAIL:-}"
ALERT_WEBHOOK="${ALERT_WEBHOOK:-}"
LOG_FILE="${LOG_FILE:-/var/log/monitrang-monitoring.log}"
CHECK_INTERVAL="${CHECK_INTERVAL:-60}"  # seconds

# Service definitions
declare -A SERVICES=(
    ["mnggateway"]="http://localhost:5000/health https://localhost:5443/health"
    ["mngkeeper"]="https://localhost:5001/health https://localhost:5001/api/version/short"
    ["mngdatagateway"]="https://localhost:5010/api/v1/health"
    ["mnghub"]="http://localhost:5020/health"
    ["mngui"]="http://localhost:3000"
)

# Alert function
send_alert() {
    local service=$1
    local status=$2
    local message="Service $service is $status at $(date)"
    
    echo "[$(date)] ALERT: $message" >> "$LOG_FILE"
    
    # Email alert (if configured)
    if [ -n "$ALERT_EMAIL" ]; then
        echo "$message" | mail -s "MonitraNG Alert: $service is $status" "$ALERT_EMAIL" 2>/dev/null || true
    fi
    
    # Webhook alert (if configured)
    if [ -n "$ALERT_WEBHOOK" ]; then
        curl -X POST "$ALERT_WEBHOOK" \
            -H "Content-Type: application/json" \
            -d "{\"service\":\"$service\",\"status\":\"$status\",\"message\":\"$message\",\"timestamp\":\"$(date -Iseconds)\"}" \
            2>/dev/null || true
    fi
    
    # Console output
    echo "⚠️  ALERT: $message"
}

# Health check function
check_service() {
    local service=$1
    shift
    local endpoints=("$@")
    
    for endpoint in "${endpoints[@]}"; do
        if [[ $endpoint == https://* ]]; then
            if curl -f -k "$endpoint" -m 5 -s -o /dev/null 2>/dev/null; then
                return 0
            fi
        else
            if curl -f "$endpoint" -m 5 -s -o /dev/null 2>/dev/null; then
                return 0
            fi
        fi
    done
    
    return 1
}

# Main monitoring loop
monitor_services() {
    echo "Starting MonitraNG Service Monitoring..."
    echo "Check interval: ${CHECK_INTERVAL} seconds"
    echo "Log file: $LOG_FILE"
    echo ""
    
    declare -A service_status
    
    while true; do
        for service in "${!SERVICES[@]}"; do
            IFS=' ' read -ra endpoints <<< "${SERVICES[$service]}"
            
            if check_service "$service" "${endpoints[@]}"; then
                if [ "${service_status[$service]}" != "healthy" ]; then
                    echo "[$(date)] ✓ $service is now healthy" | tee -a "$LOG_FILE"
                    service_status[$service]="healthy"
                fi
            else
                if [ "${service_status[$service]}" != "unhealthy" ]; then
                    send_alert "$service" "down"
                    service_status[$service]="unhealthy"
                else
                    echo "[$(date)] ✗ $service is still down" >> "$LOG_FILE"
                fi
            fi
        done
        
        sleep "$CHECK_INTERVAL"
    done
}

# Single check mode (for cron jobs)
single_check() {
    local failed_services=()
    
    for service in "${!SERVICES[@]}"; do
        IFS=' ' read -ra endpoints <<< "${SERVICES[$service]}"
        
        if ! check_service "$service" "${endpoints[@]}"; then
            failed_services+=("$service")
            send_alert "$service" "down"
        fi
    done
    
    if [ ${#failed_services[@]} -eq 0 ]; then
        echo "[$(date)] ✓ All services are healthy" >> "$LOG_FILE"
        exit 0
    else
        echo "[$(date)] ✗ Failed services: ${failed_services[*]}" >> "$LOG_FILE"
        exit 1
    fi
}

# Main
if [ "$1" == "--single" ]; then
    single_check
else
    monitor_services
fi

