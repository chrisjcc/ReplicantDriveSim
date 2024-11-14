# syntax=docker/dockerfile:1.2

# Use an official image as a base (e.g., Ubuntu)
FROM --platform=$BUILDPLATFORM alpine:latest

# Copy the Builds directory and metadata.txt file into the container at /app
COPY ./Builds /app/Builds
#COPY metadata.txt /app/metadata.txt

# Set the working directory inside the container
WORKDIR /app

# Display the contents of metadata.txt
#RUN cat metadata.txt
CMD ["/bin/sh"]
