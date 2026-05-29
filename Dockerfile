FROM cm2network/steamcmd:latest

USER root

RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        lib32gcc-s1 \
        libgdiplus \
        ca-certificates \
        curl \
        unzip \
        jq \
        gawk \
    && rm -rf /var/lib/apt/lists/*

ENV RUST_SERVER_DIR="/rust"

RUN mkdir -p ${RUST_SERVER_DIR} && \
    chown -R steam:steam ${RUST_SERVER_DIR}

COPY --chown=steam:steam entrypoint.sh /entrypoint.sh
COPY --chown=steam:steam scripts/ /scripts/
RUN chmod +x /entrypoint.sh /scripts/*.sh

# Bake the repo's seed config into the image. At runtime /cfg is a named volume
# (rust-cfg) so web-admin settings survive Dokploy redeploys; the entrypoint copies
# any missing defaults from /seed-cfg into it on first boot. Creating /cfg here owned
# by steam makes a freshly-created volume inherit that ownership, so the non-root
# entrypoint can seed it and web-admin can write settings.
COPY --chown=steam:steam cfg/ /seed-cfg/
RUN mkdir -p /cfg && chown steam:steam /cfg

USER steam

ENTRYPOINT ["/entrypoint.sh"]
