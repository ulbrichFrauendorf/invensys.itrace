#!/usr/bin/env sh
set -eu

COMPOSE_FILE="docker-compose.production.yml"
ENV_FILE=".env"

if [ -z "${DOCKER_CONFIG:-}" ]; then
	DOCKER_CONFIG="$(pwd)/.docker"
	export DOCKER_CONFIG
fi

mkdir -p "$DOCKER_CONFIG"
chmod 700 "$DOCKER_CONFIG"

get_env_value() {
	key="$1"
	value="$(sed -n "s/^${key}=//p" "$ENV_FILE" | tail -n 1)"

	case "$value" in
	\'*\')
		value="${value#\'}"
		value="${value%\'}"
		printf "%s" "$value" | sed "s/\\\\'/'/g; s/\\\\\\\\/\\\\/g"
		;;
	*)
		printf "%s" "$value"
		;;
	esac
}

docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" up -d --build --remove-orphans
docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" ps

web_port="$(get_env_value "WEB_PORT")"
if [ -z "$web_port" ]; then
	web_port="8082"
fi

health_url="http://localhost:${web_port}/health"
echo "Waiting for app health at ${health_url}"

attempt=1
while [ "$attempt" -le 60 ]; do
	if curl -fsS "$health_url" >/dev/null 2>&1; then
		echo "App health check passed"
		break
	fi

	attempt=$((attempt + 1))
	sleep 5
done

if [ "$attempt" -gt 60 ]; then
	echo "App health check failed at ${health_url}"
	docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" logs --tail=200 app
	exit 1
fi

performance_counters_url="http://localhost:${web_port}/api/performance-counters?intervalMinutes=5"
echo "Checking performance counters endpoint at ${performance_counters_url}"
if ! curl -fsS "$performance_counters_url" >/dev/null; then
	echo "Performance counters endpoint check failed at ${performance_counters_url}"
	docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" logs --tail=200 app
	exit 1
fi

docker image prune -f
