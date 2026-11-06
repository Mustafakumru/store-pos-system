# Panel imajı: Angular production derlemesi + nginx (API'ye ters vekil)
FROM node:20-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npx ng build

FROM nginx:1.27-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/lcw-pos-admin/browser /usr/share/nginx/html
EXPOSE 80
