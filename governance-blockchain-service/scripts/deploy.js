const hre = require("hardhat");
const fs = require("fs");
const path = require("path");

async function main() {
  console.log("Deploying GovernanceAuditRegistry to network:", hre.network.name);

  const [deployer] = await hre.ethers.getSigners();
  console.log("Deploying from account:", deployer.address);

  const RegistryFactory = await hre.ethers.getContractFactory("GovernanceAuditRegistry");
  const registry = await RegistryFactory.deploy({ gasPrice: 0 });
  await registry.waitForDeployment();

  const targetAddress = await registry.getAddress();
  const deployTx = registry.deploymentTransaction();
  const receipt = await deployTx.wait();

  console.log("=== DEPLOYMENT SUCCESSFUL ===");
  console.log("Contract Address:", targetAddress);
  console.log("Deployment Tx Hash:", receipt.hash);
  console.log("Deployment Block Number:", receipt.blockNumber);
  console.log("Contract Owner:", await registry.owner());

  const deploymentData = {
    network: hre.network.name,
    chainId: (await hre.ethers.provider.getNetwork()).chainId.toString(),
    contractAddress: targetAddress,
    transactionHash: receipt.hash,
    blockNumber: receipt.blockNumber,
    deployer: deployer.address,
    deployedAt: new Date().toISOString()
  };

  const outPath = path.resolve(__dirname, "../deployment.json");
  fs.writeFileSync(outPath, JSON.stringify(deploymentData, null, 2));
  console.log("Deployment details saved to:", outPath);
}

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
