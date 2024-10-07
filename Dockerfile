# syntax=docker/dockerfile:1.2

FROM --platform=$BUILDPLATFORM alpine:latest

COPY ./Builds /app/Builds
#COPY metadata.txt /app/metadata.txt

WORKDIR /app

#CMD ["cat", "metadata.txt"]
CMD ["ls", "."]
