FROM oven/bun:1

WORKDIR /app

COPY package.json bun.lock* ./
RUN bun install --frozen-lockfile 2>/dev/null || bun install

COPY . .

EXPOSE ${WEB_PORT:-3000}

CMD ["bun", "run", "src/index.ts"]
