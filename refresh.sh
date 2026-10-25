#!/bin/sh
cd infra
docker build --tag arranger -f ../src/Arranger.Server/Dockerfile ..

docker-compose down
docker-compose up -d
cd ..
