#!/bin/bash
set -e

RUST_SERVER_DIR="${RUST_SERVER_DIR:-/rust}"
OXIDE_PLUGINS_DIR="${RUST_SERVER_DIR}/oxide/plugins"
PLUGINS_FILE="/plugins/umod-plugins.txt"
PLUGIN_VERSIONS_DIR="${RUST_SERVER_DIR}/oxide/.plugin-versions"

mkdir -p "${OXIDE_PLUGINS_DIR}"
mkdir -p "${PLUGIN_VERSIONS_DIR}"

# Copy any .cs files mounted in /plugins directly
if ls /plugins/*.cs 1>/dev/null 2>&1; then
    echo "==> Copying local plugin files..."
    cp /plugins/*.cs "${OXIDE_PLUGINS_DIR}/"
    echo "==> Local plugins copied."
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
