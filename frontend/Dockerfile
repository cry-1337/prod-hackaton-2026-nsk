ARG DEPS_IMAGE=registry.spirit.t-education.com/cicd-images/example-frontend-deps:latest
FROM ${DEPS_IMAGE} AS build

WORKDIR /app

COPY package.json package-lock.json /tmp/src/
RUN if cmp -s /tmp/src/package.json package.json && cmp -s /tmp/src/package-lock.json package-lock.json; then \
      echo "deps: используем node_modules из образа"; \
    else \
      echo "deps: package*.json изменились, выполняем npm ci"; \
      cp /tmp/src/package.json /tmp/src/package-lock.json ./ && npm ci; \
    fi

COPY . .

RUN npm run build

FROM caddy:2-alpine

COPY deploy/Caddyfile /etc/caddy/Caddyfile
COPY --from=build /app/dist/ /srv/
