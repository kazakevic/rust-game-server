.PHONY: build up down restart logs rcon shell clean

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

clean:
	docker compose down -v
