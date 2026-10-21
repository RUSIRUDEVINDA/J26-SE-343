require("@nomicfoundation/hardhat-toolbox");

/** @type import('hardhat/config').HardhatUserConfig */
module.exports = {
  solidity: {
    version: "0.8.20",
    settings: {
      optimizer: {
        enabled: true,
        runs: 200,
      },
    },
  },
  networks: {
    hardhat: {},
    besu: {
      url: process.env.BESU_RPC_URL || "http://127.0.0.1:8545",
      chainId: 1337,
      gasPrice: 0,
      accounts: process.env.BESU_DEPLOYER_PRIVATE_KEY
        ? [process.env.BESU_DEPLOYER_PRIVATE_KEY]
        : ["0x8f2a55949038a9610f50fb23b5883af3b4ecb3c3bb792cbcefbd1542c692be63"],
    },
    besu_local: {
      url: process.env.BESU_RPC_URL || "http://127.0.0.1:8545",
      chainId: 1337,
      gasPrice: 0,
      accounts: process.env.BESU_DEPLOYER_PRIVATE_KEY
        ? [process.env.BESU_DEPLOYER_PRIVATE_KEY]
        : ["0x8f2a55949038a9610f50fb23b5883af3b4ecb3c3bb792cbcefbd1542c692be63"],
    },
  },
  paths: {
    sources: "./contracts",
    tests: "./tests/contracts",
    cache: "./cache",
    artifacts: "./artifacts",
  },
};
