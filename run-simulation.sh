#!/bin/sh

set -e

PROJECT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
DOTNET_DIR="${DOTNET_DIR:-${PROJECT_DIR}/../.dotnet}"
SUPPORT_DIR="${OPENHV_SUPPORT_DIR:-${PROJECT_DIR}/../.openhv-support}"
SIMULATION_MAP="${1:-coldrage}"
SIMULATION_BOT="${SIMULATION_BOT:-rogue}"
SIMULATION_BOTS="${SIMULATION_BOTS:-${SIMULATION_BOT}}"
SIMULATION_SPEED="${SIMULATION_SPEED:-fastest}"
SIMULATION_SEED="${SIMULATION_SEED:-}"
SIMULATION_DURATION="${SIMULATION_DURATION:-0}"
SIMULATION_RESULT="${SIMULATION_RESULT:-}"

if [ ! -x "${DOTNET_DIR}/dotnet" ]; then
	echo "Local .NET SDK not found at ${DOTNET_DIR}." >&2
	exit 1
fi

mkdir -p "${SUPPORT_DIR}"

PATH="${DOTNET_DIR}:${PATH}" \
DOTNET_ROOT="${DOTNET_DIR}" \
DOTNET_CLI_TELEMETRY_OPTOUT=1 \
exec "${PROJECT_DIR}/launch-game.sh" \
	"Engine.SupportDir=${SUPPORT_DIR}" \
	"Launch.Simulation=true" \
	"Launch.Map=${SIMULATION_MAP}" \
	"Launch.SimulationBot=${SIMULATION_BOT}" \
	"Launch.SimulationBots=${SIMULATION_BOTS}" \
	"Launch.SimulationSpeed=${SIMULATION_SPEED}" \
	"Launch.SimulationSeed=${SIMULATION_SEED}" \
	"Launch.SimulationDuration=${SIMULATION_DURATION}" \
	"Launch.SimulationResult=${SIMULATION_RESULT}"
