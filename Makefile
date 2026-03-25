.PHONY: build up down restart logs rcon shell clean plugins

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

plugins:
	docker compose exec rust-server /scripts/install-plugins.sh

clean:
	docker compose down -v
