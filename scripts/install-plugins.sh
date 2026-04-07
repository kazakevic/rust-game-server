#!/bin/bash
set -e

RUST_SERVER_DIR="${RUST_SERVER_DIR:-/rust}"
OXIDE_PLUGINS_DIR="${RUST_SERVER_DIR}/oxide/plugins"
PLUGINS_FILE="/plugins/umod-plugins.txt"
PLUGIN_VERSIONS_DIR="${RUST_SERVER_DIR}/oxide/.plugin-versions"

mkdir -p "${OXIDE_PLUGINS_DIR}"
mkdir -p "${PLUGIN_VERSIONS_DIR}"

# Sync local .cs plugins — copy new/updated, remove ones no longer present
LOCAL_PLUGINS_MANIFEST="${RUST_SERVER_DIR}/oxide/.local-plugins"
touch "${LOCAL_PLUGINS_MANIFEST}"

# Remove plugins that were previously installed locally but are no longer in /plugins
while IFS= read -r installed_name; do
    [ -z "$installed_name" ] && continue
    if [ ! -f "/plugins/${installed_name}.cs" ]; then
        echo "  -> Removing local plugin no longer present: ${installed_name}"
        rm -f "${OXIDE_PLUGINS_DIR}/${installed_name}.cs"
    fi
done < "${LOCAL_PLUGINS_MANIFEST}"

if ls /plugins/*.cs 1>/dev/null 2>&1; then
    echo "==> Syncing local plugin files..."
    > "${LOCAL_PLUGINS_MANIFEST}"
    for src in /plugins/*.cs; do
        name=$(basename "$src" .cs)
        cp "$src" "${OXIDE_PLUGINS_DIR}/"
        echo "$name" >> "${LOCAL_PLUGINS_MANIFEST}"
    done
    echo "==> Local plugins synced."
else
    > "${LOCAL_PLUGINS_MANIFEST}"
fi

# Download plugins listed in umod-plugins.txt
if [ -f "${PLUGINS_FILE}" ]; then
    echo "==> Checking plugins from umod.org..."

    while IFS= read -r line || [ -n "$line" ]; do
        # Skip empty lines and comments
        line=$(echo "$line" | sed 's/#.*//' | xargs)
        [ -z "$line" ] && continue

        PLUGIN_NAME="$line"

        # Get plugin metadata from umod API
        PLUGIN_JSON=$(curl -fsSL "https://umod.org/plugins/${PLUGIN_NAME}.json" 2>/dev/null) || {
            echo "  WARNING: Plugin '${PLUGIN_NAME}' not found on umod.org, skipping."
            continue
        }

        DOWNLOAD_URL=$(echo "$PLUGIN_JSON" | jq -r '.download_url // empty')
        VERSION=$(echo "$PLUGIN_JSON" | jq -r '.latest_release_version // "unknown"')

        if [ -z "$DOWNLOAD_URL" ]; then
            echo "  WARNING: No download URL for '${PLUGIN_NAME}', skipping."
            continue
        fi

        # Check if plugin is already installed at this version
        VERSION_FILE="${PLUGIN_VERSIONS_DIR}/${PLUGIN_NAME}.version"
        if [ -f "$VERSION_FILE" ] && [ -f "${OXIDE_PLUGINS_DIR}/${PLUGIN_NAME}.cs" ]; then
            INSTALLED_VERSION=$(cat "$VERSION_FILE")
            if [ "$INSTALLED_VERSION" = "$VERSION" ]; then
                echo "  -> ${PLUGIN_NAME} v${VERSION} already installed, skipping."
                continue
            fi
            echo "  -> ${PLUGIN_NAME} update available: v${INSTALLED_VERSION} -> v${VERSION}"
        else
            echo "  -> Fetching ${PLUGIN_NAME} v${VERSION}..."
        fi

        if curl -fsSL -o "${OXIDE_PLUGINS_DIR}/${PLUGIN_NAME}.cs" "$DOWNLOAD_URL"; then
            echo "$VERSION" > "$VERSION_FILE"
            echo "  -> ${PLUGIN_NAME} v${VERSION} installed."
        else
            echo "  WARNING: Failed to download '${PLUGIN_NAME}'."
        fi
    done < "${PLUGINS_FILE}"

    echo "==> Plugin check complete."
else
    echo "==> No umod-plugins.txt found, skipping plugin downloads."
fi
