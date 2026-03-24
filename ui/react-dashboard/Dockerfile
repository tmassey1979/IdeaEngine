FROM node:22-alpine AS build
WORKDIR /app

COPY ui/react-dashboard/package*.json ./
RUN npm ci

COPY ui/react-dashboard/ ./
RUN npm run build

FROM nginx:1.27-alpine
COPY ui/react-dashboard/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
