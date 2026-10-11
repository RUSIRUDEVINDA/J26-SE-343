package main

import (
	"context"
	"fmt"
	"log"
	"net/http"
	"os"
	"os/signal"
	"syscall"
	"time"

	"governance-blockchain-service/internal/anchors"
	"governance-blockchain-service/internal/api"
	"governance-blockchain-service/internal/besu"
	"governance-blockchain-service/internal/config"
)

func main() {
	cfg := config.Load()

	log.Printf("[INFO] Initializing Governance Blockchain Service...")
	log.Printf("[INFO] Blockchain Mode: %s", cfg.BlockchainMode)
	log.Printf("[INFO] Network Target: Hyperledger Besu (QBFT) on %s (ChainID: %d)", cfg.BesuRPCURL, cfg.ChainID)
	log.Printf("[INFO] Smart Contract Address: %s", cfg.ContractAddress)

	var ledger besu.LedgerClient

	if cfg.BlockchainMode == "mock" {
		log.Printf("[INFO] Explicit Mock Mode: BLOCKCHAIN_MODE=mock (SimulateOffline: %v)", cfg.SimulateOffline)
		ledger = besu.NewMockLedgerClient("", cfg.SimulateOffline)
	} else {
		log.Printf("[INFO] Normal Mode: Connecting to Hyperledger Besu RPC endpoint at %s...", cfg.BesuRPCURL)
		realClient, err := besu.NewRealBesuClient(cfg.BesuRPCURL, cfg.ContractAddress, cfg.PrivateKeyHex, cfg.ChainID)
		if err != nil {
			log.Printf("[WARN] Failed to connect to Besu network: %v. Safe fail-closed mode engaged (Unavailable).", err)
			ledger = besu.NewUnavailableLedgerClient(err.Error())
		} else {
			log.Printf("[INFO] Connected successfully to Besu RPC node. Contract verified at %s.", cfg.ContractAddress)
			ledger = realClient
		}
	}

	anchorService := anchors.NewService(ledger)
	handler := api.NewHandler(anchorService, ledger, "Hyperledger Besu (QBFT)", cfg.ChainID)
	router := api.SetupRouter(handler)

	addr := fmt.Sprintf(":%d", cfg.Port)
	server := &http.Server{
		Addr:         addr,
		Handler:      router,
		ReadTimeout:  10 * time.Second,
		WriteTimeout: 10 * time.Second,
	}

	go func() {
		log.Printf("[INFO] Governance Blockchain Service listening on http://localhost:%d", cfg.Port)
		if err := server.ListenAndServe(); err != nil && err != http.ErrServerClosed {
			log.Fatalf("[FATAL] HTTP server error: %v", err)
		}
	}()

	// Graceful shutdown
	quit := make(chan os.Signal, 1)
	signal.Notify(quit, syscall.SIGINT, syscall.SIGTERM)
	<-quit

	log.Printf("[INFO] Shutting down Governance Blockchain Service...")
	ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
	defer cancel()

	if err := server.Shutdown(ctx); err != nil {
		log.Fatalf("[ERROR] Server forced to shutdown: %v", err)
	}
	log.Printf("[INFO] Server exiting cleanly.")
}
