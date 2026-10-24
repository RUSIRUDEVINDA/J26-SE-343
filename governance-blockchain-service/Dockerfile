FROM golang:1.22-alpine AS builder

WORKDIR /app

COPY go.mod go.sum ./
RUN go mod download

COPY . ./
RUN CGO_ENABLED=0 GOOS=linux go build -o /bin/governance-blockchain-service ./cmd/api

FROM alpine:3.20

RUN apk --no-cache add ca-certificates

WORKDIR /app
COPY --from=builder /bin/governance-blockchain-service /app/governance-blockchain-service

EXPOSE 8550

ENTRYPOINT ["/app/governance-blockchain-service"]
