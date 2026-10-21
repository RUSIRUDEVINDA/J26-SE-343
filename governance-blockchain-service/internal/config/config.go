package config

import (
	"encoding/json"
	"os"
	"path/filepath"
	"strconv"
)

// Config represents runtime configuration loaded from environment variables or deployment artifacts.
type Config struct {
	Port            int
	BesuRPCURL      string
	ContractAddress string
	PrivateKeyHex   string
	ChainID         int64
	BlockchainMode  string // "besu" (default) or "mock"
	SimulateOffline bool
}

type deploymentArtifact struct {
	ContractAddress string `json:"contractAddress"`
}

// Load reads configuration from environment variables with dynamic deployment artifact detection.
func Load() *Config {
	port, _ := strconv.Atoi(getEnv("PORT", "8550"))
	chainID, _ := strconv.ParseInt(getEnv("BESU_CHAIN_ID", "1337"), 10, 64)
	simulateOffline := getEnv("SIMULATE_LEDGER_OFFLINE", "false") == "true"
	blockchainMode := getEnv("BLOCKCHAIN_MODE", "besu")

	contractAddr := os.Getenv("REGISTRY_CONTRACT_ADDRESS")
	if contractAddr == "" {
		// Attempt dynamic discovery from deployment.json
		contractAddr = discoverDeployedAddress()
	}

	return &Config{
		Port:            port,
		BesuRPCURL:      getEnv("BESU_RPC_URL", "http://127.0.0.1:8545"),
		ContractAddress: contractAddr,
		PrivateKeyHex:   getEnv("BESU_PRIVATE_KEY", "8f2a55949038a9610f50fb23b5883af3b4ecb3c3bb792cbcefbd1542c692be63"),
		ChainID:         chainID,
		BlockchainMode:  blockchainMode,
		SimulateOffline: simulateOffline,
	}
}

func discoverDeployedAddress() string {
	paths := []string{"deployment.json", "../deployment.json", "../../deployment.json"}
	for _, p := range paths {
		abs, err := filepath.Abs(p)
		if err == nil {
			data, err := os.ReadFile(abs)
			if err == nil {
				var artifact deploymentArtifact
				if err := json.Unmarshal(data, &artifact); err == nil && artifact.ContractAddress != "" {
					return artifact.ContractAddress
				}
			}
		}
	}
	return ""
}

func getEnv(key, defaultVal string) string {
	if val := os.Getenv(key); val != "" {
		return val
	}
	return defaultVal
}
