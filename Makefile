.PHONY: build up down restart logs rcon shell clean plugins reload update update-umod

build:
	docker compose build

up:
	docker compose up -d

down:
	docker compose down

restart:
	docker compose restart

logs:
	docker compose logs -f rust-server

rcon:
	docker compose exec rust-server bash

shell:
	docker compose exec rust-server bash

update:
	docker compose down
	RUST_UPDATE_ON_START=1 docker compose up -d

plugins:
	docker compose exec rust-server /scripts/install-plugins.sh

reload:
	docker compose exec rust-server bash -c 'cp /plugins/*.cs /rust/oxide/plugins/ && echo "Plugins copied — Oxide will auto-reload."'

update-umod:
	docker compose exec rust-server /scripts/update-umod.sh

clean:
	docker compose down -v
