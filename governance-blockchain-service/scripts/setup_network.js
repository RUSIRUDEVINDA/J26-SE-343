const fs = require('fs');
const path = require('path');
const { ethers } = require('ethers');

const besuDir = path.resolve(__dirname, '../network/besu');
const keysDir = path.join(besuDir, 'keys');
const nodesDir = path.join(besuDir, 'nodes');

// Ensure nodes dir exists
fs.mkdirSync(nodesDir, { recursive: true });

// Read the 4 validator addresses
const validatorAddresses = fs.readdirSync(keysDir).filter(name => name.startsWith('0x'));
console.log('Found validator addresses:', validatorAddresses);

const validators = [];
validatorAddresses.forEach((addr, idx) => {
  const dir = path.join(keysDir, addr);
  const privKey = fs.readFileSync(path.join(dir, 'key.priv'), 'utf8').trim();
  const pubKey = fs.readFileSync(path.join(dir, 'key.pub'), 'utf8').trim();
  
  const valDir = path.join(nodesDir, `validator${idx + 1}`);
  fs.mkdirSync(valDir, { recursive: true });
  fs.writeFileSync(path.join(valDir, 'key'), privKey);
  fs.writeFileSync(path.join(valDir, 'key.pub'), pubKey);

  validators.push({
    name: `validator${idx + 1}`,
    address: addr,
    ip: `172.29.0.${idx + 2}`,
    port: 30303,
    pubKey: pubKey.replace(/^0x/, '')
  });
});

// Generate RPC node key
const rpcWallet = ethers.Wallet.createRandom();
const rpcPrivKey = rpcWallet.privateKey.replace(/^0x/, '');
const rpcPubKey = ethers.SigningKey.computePublicKey(rpcWallet.privateKey, false).replace(/^0x04/, '');
const rpcDir = path.join(nodesDir, 'rpc-node');
fs.mkdirSync(rpcDir, { recursive: true });
fs.writeFileSync(path.join(rpcDir, 'key'), rpcPrivKey);
fs.writeFileSync(path.join(rpcDir, 'key.pub'), '0x' + rpcPubKey);

const rpcNode = {
  name: 'rpc-node',
  address: rpcWallet.address.toLowerCase(),
  ip: '172.29.0.6',
  port: 30303,
  pubKey: rpcPubKey
};

const allNodes = [...validators, rpcNode];

// Build nodes allowlist
const nodesAllowlist = allNodes.map(n => `"enode://${n.pubKey}@${n.ip}:${n.port}"`);

// Build accounts allowlist (deployer, test submitter, validators)
const accountsAllowlist = [
  '"0xfe3b557e8fb62b89f4916b721be55ceb828dbd73"', // Deployer / Pre-funded dev account
  '"0x8f2a55949038a9610f50fb23b5883af3b4ecb3c3"', // Submitter account
  ...validators.map(v => `"${v.address.toLowerCase()}"`)
];

const permissionsToml = `# Hyperledger Besu Development Network Permissioning Configuration
# Enforces both Node-level and Account-level allowlists

# Node Permissioning
nodes-allowlist = [
  ${nodesAllowlist.join(',\n  ')}
]

# Account Permissioning
accounts-allowlist = [
  ${accountsAllowlist.join(',\n  ')}
]
`;

fs.writeFileSync(path.join(besuDir, 'permissions_config.toml'), permissionsToml);
console.log('Generated permissions_config.toml successfully.');

// Write docker-compose.yml
const bootnodeEnode = `enode://${validators[0].pubKey}@${validators[0].ip}:${validators[0].port}`;

const dockerCompose = `services:
  validator1:
    image: hyperledger/besu:26.9.0
    container_name: besu-validator1
    environment:
      - BESU_LOGGING=INFO
    volumes:
      - .:/opt/besu/work:rw
      - validator1-data:/opt/besu/data
    command:
      - --data-path=/opt/besu/data
      - --genesis-file=/opt/besu/work/genesis.json
      - --node-private-key-file=/opt/besu/work/nodes/validator1/key
      - --p2p-port=30303
      - --p2p-host=172.29.0.2
      - --rpc-http-enabled=false
      - --min-gas-price=0
      - --permissions-nodes-config-file-enabled=true
      - --permissions-nodes-config-file=/opt/besu/work/permissions_config.toml
      - --permissions-accounts-config-file-enabled=true
      - --permissions-accounts-config-file=/opt/besu/work/permissions_config.toml
    networks:
      besu-network:
        ipv4_address: 172.29.0.2

  validator2:
    image: hyperledger/besu:26.9.0
    container_name: besu-validator2
    environment:
      - BESU_LOGGING=INFO
    volumes:
      - .:/opt/besu/work:rw
      - validator2-data:/opt/besu/data
    command:
      - --data-path=/opt/besu/data
      - --genesis-file=/opt/besu/work/genesis.json
      - --node-private-key-file=/opt/besu/work/nodes/validator2/key
      - --p2p-port=30303
      - --p2p-host=172.29.0.3
      - --bootnodes=${bootnodeEnode}
      - --rpc-http-enabled=false
      - --min-gas-price=0
      - --permissions-nodes-config-file-enabled=true
      - --permissions-nodes-config-file=/opt/besu/work/permissions_config.toml
      - --permissions-accounts-config-file-enabled=true
      - --permissions-accounts-config-file=/opt/besu/work/permissions_config.toml
    networks:
      besu-network:
        ipv4_address: 172.29.0.3
    depends_on:
      - validator1

  validator3:
    image: hyperledger/besu:26.9.0
    container_name: besu-validator3
    environment:
      - BESU_LOGGING=INFO
    volumes:
      - .:/opt/besu/work:rw
      - validator3-data:/opt/besu/data
    command:
      - --data-path=/opt/besu/data
      - --genesis-file=/opt/besu/work/genesis.json
      - --node-private-key-file=/opt/besu/work/nodes/validator3/key
      - --p2p-port=30303
      - --p2p-host=172.29.0.4
      - --bootnodes=${bootnodeEnode}
      - --rpc-http-enabled=false
      - --min-gas-price=0
      - --permissions-nodes-config-file-enabled=true
      - --permissions-nodes-config-file=/opt/besu/work/permissions_config.toml
      - --permissions-accounts-config-file-enabled=true
      - --permissions-accounts-config-file=/opt/besu/work/permissions_config.toml
    networks:
      besu-network:
        ipv4_address: 172.29.0.4
    depends_on:
      - validator1

  validator4:
    image: hyperledger/besu:26.9.0
    container_name: besu-validator4
    environment:
      - BESU_LOGGING=INFO
    volumes:
      - .:/opt/besu/work:rw
      - validator4-data:/opt/besu/data
    command:
      - --data-path=/opt/besu/data
      - --genesis-file=/opt/besu/work/genesis.json
      - --node-private-key-file=/opt/besu/work/nodes/validator4/key
      - --p2p-port=30303
      - --p2p-host=172.29.0.5
      - --bootnodes=${bootnodeEnode}
      - --rpc-http-enabled=false
      - --min-gas-price=0
      - --permissions-nodes-config-file-enabled=true
      - --permissions-nodes-config-file=/opt/besu/work/permissions_config.toml
      - --permissions-accounts-config-file-enabled=true
      - --permissions-accounts-config-file=/opt/besu/work/permissions_config.toml
    networks:
      besu-network:
        ipv4_address: 172.29.0.5
    depends_on:
      - validator1

  rpc-node:
    image: hyperledger/besu:26.9.0
    container_name: besu-rpc-node
    environment:
      - BESU_LOGGING=INFO
    volumes:
      - .:/opt/besu/work:rw
      - rpc-data:/opt/besu/data
    command:
      - --data-path=/opt/besu/data
      - --genesis-file=/opt/besu/work/genesis.json
      - --node-private-key-file=/opt/besu/work/nodes/rpc-node/key
      - --p2p-port=30303
      - --p2p-host=172.29.0.6
      - --bootnodes=${bootnodeEnode}
      - --rpc-http-enabled=true
      - --rpc-http-host=0.0.0.0
      - --rpc-http-port=8545
      - --rpc-http-api=ETH,NET,QBFT,WEB3
      - --rpc-http-cors-origins=*
      - --host-allowlist=*
      - --min-gas-price=0
      - --permissions-nodes-config-file-enabled=true
      - --permissions-nodes-config-file=/opt/besu/work/permissions_config.toml
      - --permissions-accounts-config-file-enabled=true
      - --permissions-accounts-config-file=/opt/besu/work/permissions_config.toml
    ports:
      - "8545:8545"
    networks:
      besu-network:
        ipv4_address: 172.29.0.6
    depends_on:
      - validator1
      - validator2
      - validator3
      - validator4

networks:
  besu-network:
    driver: bridge
    ipam:
      config:
        - subnet: 172.29.0.0/16

volumes:
  validator1-data:
  validator2-data:
  validator3-data:
  validator4-data:
  rpc-data:
`;

fs.writeFileSync(path.join(besuDir, 'docker-compose.yml'), dockerCompose);
console.log('Generated docker-compose.yml successfully.');
