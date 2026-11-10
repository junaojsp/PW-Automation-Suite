FROM node:22-alpine
WORKDIR /srv/app
COPY package.json ./
RUN npm install --omit=dev --no-audit --no-fund && npm cache clean --force
COPY server.js index.html ./
USER node
EXPOSE 3000
CMD ["node", "server.js"]
