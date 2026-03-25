FROM cm2network/steamcmd:latest

USER root

RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        lib32gcc-s1 \
        libgdiplus \
        ca-certificates \
    && rm -rf /var/lib/apt/lists/*

ENV RUST_SERVER_DIR="/rust"

RUN mkdir -p ${RUST_SERVER_DIR} && \
    chown -R steam:steam ${RUST_SERVER_DIR}

COPY --chown=steam:steam entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

USER steam

ENTRYPOINT ["/entrypoint.sh"]
