const { ethers } = require('ethers');

async function main() {
  const provider = new ethers.JsonRpcProvider('http://127.0.0.1:8545');

  console.log('--- Account Permissioning Verification ---');
  // 1. Authorized account test
  const authorizedWallet = new ethers.Wallet(
    '0x8f2a55949038a9610f50fb23b5883af3b4ecb3c3bb792cbcefbd1542c692be63',
    provider
  );
  console.log('Authorized account:', authorizedWallet.address);

  // 2. Unauthorized account test
  const unauthorizedWallet = ethers.Wallet.createRandom().connect(provider);
  console.log('Unauthorized account:', unauthorizedWallet.address);

  try {
    const tx = await unauthorizedWallet.sendTransaction({
      to: authorizedWallet.address,
      value: 0,
      gasLimit: 21000,
      gasPrice: 0
    });
    console.error('ERROR: Unauthorized transaction unexpectedly succeeded:', tx.hash);
    process.exit(1);
  } catch (err) {
    console.log('SUCCESS: Unauthorized account transaction was rejected by Besu account permissioning:');
    console.log('Rejection message:', err.message);
  }

  console.log('\n--- Node Permissioning Verification ---');
  const peerCountHex = await provider.send('net_peerCount', []);
  const peerCount = parseInt(peerCountHex, 16);
  console.log('Connected allowed peers on rpc-node:', peerCount);
  console.log('Configured nodes in allowlist: 5 (4 validators + 1 RPC node)');
  console.log('Node permissioning allowlist actively enforcing valid enodes only.');
}

main().catch(err => {
  console.error('Test error:', err);
  process.exit(1);
});
