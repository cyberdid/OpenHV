#!/bin/sh

set -e

PROJECT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)

# shellcheck source=mod.config
. "${PROJECT_DIR}/mod.config"
if [ -f "${PROJECT_DIR}/user.config" ]; then
	# shellcheck source=user.config
	. "${PROJECT_DIR}/user.config"
fi

ENGINE_PATH="${PROJECT_DIR}/${ENGINE_DIRECTORY#./}"
PATCH_PATH="${PROJECT_DIR}/engine-patches/openra-headless.patch"
AI_COMBAT_PATCH_PATH="${PROJECT_DIR}/engine-patches/openra-ai-combat.patch"
OVERLAY_SOURCE="${PROJECT_DIR}/engine-patches/OpenRA.Game/Graphics/HeadlessPlatform.cs"
OVERLAY_TARGET="${ENGINE_PATH}/OpenRA.Game/Graphics/HeadlessPlatform.cs"
BOT_MODULES_PATH="${ENGINE_PATH}/OpenRA.Mods.Common/Traits/BotModules"

if [ ! -f "${ENGINE_PATH}/VERSION" ]; then
	echo "Cannot apply engine patches: ${ENGINE_PATH} is not an initialized OpenRA SDK." >&2
	exit 1
fi

apply_patch() {
	if git -C "${PROJECT_DIR}" apply --check --directory="${ENGINE_DIRECTORY#./}" "$1" 2>/dev/null; then
		git -C "${PROJECT_DIR}" apply --directory="${ENGINE_DIRECTORY#./}" "$1"
	elif ! git -C "${PROJECT_DIR}" apply --reverse --check --directory="${ENGINE_DIRECTORY#./}" "$1" 2>/dev/null; then
		echo "Engine patch $1 does not apply cleanly to ${ENGINE_PATH}." >&2
		exit 1
	fi
}

apply_patch "${PATCH_PATH}"

mkdir -p "$(dirname "${OVERLAY_TARGET}")"
cp "${OVERLAY_SOURCE}" "${OVERLAY_TARGET}"

# OpenRA's stock bot modules use LocalRandom, which is also consumed by
# renderer-only effects. Route bot choices through the simulation-owned stream
# so graphical and headless execution consume the same strategic randomness.
#
# Some engine sources are not valid UTF-8 (AirStates.cs carries ISO-8859 bytes).
# The locale-aware tools classify those files as binary and silently skip them,
# which used to leave air squads consuming the renderer stream while the guard
# below still reported success. Force byte semantics for every text tool here.
find "${BOT_MODULES_PATH}" -type f -name '*.cs' -print |
	while IFS= read -r bot_module; do
		if LC_ALL=C grep -a -q '\.LocalRandom' "${bot_module}"; then
			temporary="${bot_module}.universe.tmp"
			LC_ALL=C sed 's/\.LocalRandom/\.BotRandom/g' "${bot_module}" > "${temporary}"
			mv "${temporary}" "${bot_module}"
		fi
	done

if LC_ALL=C grep -a -R --include='*.cs' '\.LocalRandom' "${BOT_MODULES_PATH}" >/dev/null 2>&1 ||
	! LC_ALL=C grep -a -R --include='*.cs' '\.BotRandom' "${BOT_MODULES_PATH}" >/dev/null 2>&1; then
	echo "Failed to route OpenRA bot modules through World.BotRandom." >&2
	exit 1
fi

# The combat planner patch is generated against the BotRandom-routed tree, so it
# must be applied after the rewrite above rather than with the headless patch.
apply_patch "${AI_COMBAT_PATCH_PATH}"
