using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using NBitcoin.DataEncoders;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Features.SmartContracts.PoA;
using Stratis.SmartContracts.Networks.Policies;

namespace Stratis.Sidechains.Networks
{
    /// <summary>
    /// Right now, ripped nearly straight from <see cref="PoANetwork"/>.
    /// </summary>
    public class CirrusMain : PoANetwork
    {
        /// <summary> The name of the root folder containing the different federated peg blockchains.</summary>
        private const string NetworkRootFolderName = "cirrus";

        /// <summary> The default name used for the federated peg configuration file. </summary>
        private const string NetworkDefaultConfigFilename = "cirrus.conf";

        internal CirrusMain()
        {
            this.Name = "CirrusMain";
            this.NetworkType = NetworkType.Mainnet;
            this.CoinTicker = "CRS";
            this.Magic = 0x522357A0;
            this.DefaultPort = 16179;
            this.DefaultMaxOutboundConnections = 16;
            this.DefaultMaxInboundConnections = 109;
            this.DefaultRPCPort = 16175;
            this.DefaultAPIPort = 37223;
            this.MaxTipAge = 2 * 60 * 60;
            this.MinTxFee = 10000;
            this.FallbackFee = 10000;
            this.MinRelayTxFee = 10000;
            this.RootFolderName = NetworkRootFolderName;
            this.DefaultConfigFilename = NetworkDefaultConfigFilename;
            this.MaxTimeOffsetSeconds = 25 * 60;

            var consensusFactory = new SmartContractCollateralPoAConsensusFactory();

            // Create the genesis block.
            this.GenesisTime = 1545310504;
            this.GenesisNonce = 761900;
            this.GenesisBits = new Target(new uint256("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"));
            this.GenesisVersion = 1;
            this.GenesisReward = Money.Zero;

            string coinbaseText = "https://www.abc.net.au/news/science/2018-12-07/encryption-bill-australian-technology-industry-fuming-mad/10589962";
            Block genesisBlock = CirrusNetwork.CreateGenesis(consensusFactory, this.GenesisTime, this.GenesisNonce, this.GenesisBits, this.GenesisVersion, this.GenesisReward, coinbaseText);

            this.Genesis = genesisBlock;

            // Configure federation public keys used to sign blocks.
            // Keep in mind that order in which keys are added to this list is important
            // and should be the same for all nodes operating on this network.
            var genesisFederationMembers = new List<IFederationMember>()
            {
                new CollateralFederationMember(new Mnemonic("basic exotic crack drink left judge tourist giggle muscle unique horn body").DeriveExtKey().PrivateKey.PubKey, new Money(10000_00000000), "SgKKjTJBAEk1WXMYTeR5XHCF1cVsAFR8D8"),
                new CollateralFederationMember(new Mnemonic("describe supreme leopard face usage post solid cruel awful empty airport chimney").DeriveExtKey().PrivateKey.PubKey, new Money(10000_00000000), "SfbPKnBrTn2GJb7gntBPM4JUGjyHHtDTnZ"),
                new CollateralFederationMember(new Mnemonic("unusual nasty narrow canal suggest humor idea leisure purity supreme naive stool").DeriveExtKey().PrivateKey.PubKey, new Money(10000_00000000), "SU7jUGKMswDyQw2GNjUX6VzgDCEaP93X1f"),
                new CollateralFederationMember(new Mnemonic("exchange fly diamond aware relax wisdom test primary dice lawn bulb cloth").DeriveExtKey().PrivateKey.PubKey, new Money(10000_00000000), "ST4eHJ48Gi4oDq9CiAqYPVRyoarsxUY9hA"),
                new CollateralFederationMember(new Mnemonic("rose volcano clay service wing meat own option chest guide drastic afraid").DeriveExtKey().PrivateKey.PubKey, new Money(10000_00000000), "STZ5Dm57BPbHgRYjaCffzVhvhF6VsB2z1S"),
                new CollateralFederationMember(new Mnemonic("into tonight barrel luxury world chunk marble ask into gauge dwarf hurry").DeriveExtKey().PrivateKey.PubKey, new Money(10000_00000000), "SMtKJKo5dThxNJhEf7fZggpoYdzZn21zpa"),
                new CollateralFederationMember(new Mnemonic("people diet stock monitor swear kid cause raven purse purity auction junior").DeriveExtKey().PrivateKey.PubKey, new Money(10000_00000000), "SMQL9PvggC5cVTj3JRN6Wjjddz8RTposUS"),
                new CollateralFederationMember(new Mnemonic("split dinosaur torch scrub sick reveal swear trend blue fit impulse vehicle").DeriveExtKey().PrivateKey.PubKey, new Money(0), null)
            };

            var consensusOptions = new PoAConsensusOptions(
                maxBlockBaseSize: 1_000_000,
                maxStandardVersion: 2,
                maxStandardTxWeight: 100_000,
                maxBlockSigopsCost: 20_000,
                maxStandardTxSigopsCost: 20_000 / 5,
                genesisFederationMembers: genesisFederationMembers,
                targetSpacingSeconds: 16,
                votingEnabled: true,
                autoKickIdleMembers: false
            );

            var buriedDeployments = new BuriedDeploymentsArray
            {
                [BuriedDeployments.BIP34] = 0,
                [BuriedDeployments.BIP65] = 0,
                [BuriedDeployments.BIP66] = 0
            };

            var bip9Deployments = new NoBIP9Deployments();

            this.Consensus = new Consensus(
                consensusFactory: consensusFactory,
                consensusOptions: consensusOptions,
                coinType: 401,
                hashGenesisBlock: genesisBlock.GetHash(),
                subsidyHalvingInterval: 210000,
                majorityEnforceBlockUpgrade: 750,
                majorityRejectBlockOutdated: 950,
                majorityWindow: 1000,
                buriedDeployments: buriedDeployments,
                bip9Deployments: bip9Deployments,
                bip34Hash: new uint256("0x000000000000024b89b42a942fe0d9fea3bb44ab7bd1b19115dd6a759c0808b8"),
                ruleChangeActivationThreshold: 1916, // 95% of 2016
                minerConfirmationWindow: 2016, // nPowTargetTimespan / nPowTargetSpacing
                maxReorgLength: 240, // Heuristic. Roughly 2 * mining members
                defaultAssumeValid: null,
                maxMoney: Money.Coins(100_000_000),
                coinbaseMaturity: 1,
                premineHeight: 2,
                premineReward: Money.Coins(100_000_000),
                proofOfWorkReward: Money.Coins(0),
                powTargetTimespan: TimeSpan.FromDays(14), // two weeks
                powTargetSpacing: TimeSpan.FromMinutes(1),
                powAllowMinDifficultyBlocks: false,
                posNoRetargeting: false,
                powNoRetargeting: true,
                powLimit: null,
                minimumChainWork: null,
                isProofOfStake: false,
                lastPowBlock: 0,
                proofOfStakeLimit: null,
                proofOfStakeLimitV2: null,
                proofOfStakeReward: Money.Zero
            );

            // Same as current smart contracts test networks to keep tests working
            this.Base58Prefixes = new byte[12][];
            this.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS] = new byte[] { 28 }; // C
            this.Base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS] = new byte[] { 88 }; // c
            this.Base58Prefixes[(int)Base58Type.SECRET_KEY] = new byte[] { (239) };
            this.Base58Prefixes[(int)Base58Type.ENCRYPTED_SECRET_KEY_NO_EC] = new byte[] { 0x01, 0x42 };
            this.Base58Prefixes[(int)Base58Type.ENCRYPTED_SECRET_KEY_EC] = new byte[] { 0x01, 0x43 };
            this.Base58Prefixes[(int)Base58Type.EXT_PUBLIC_KEY] = new byte[] { (0x04), (0x35), (0x87), (0xCF) };
            this.Base58Prefixes[(int)Base58Type.EXT_SECRET_KEY] = new byte[] { (0x04), (0x35), (0x83), (0x94) };
            this.Base58Prefixes[(int)Base58Type.PASSPHRASE_CODE] = new byte[] { 0x2C, 0xE9, 0xB3, 0xE1, 0xFF, 0x39, 0xE2 };
            this.Base58Prefixes[(int)Base58Type.CONFIRMATION_CODE] = new byte[] { 0x64, 0x3B, 0xF6, 0xA8, 0x9A };
            this.Base58Prefixes[(int)Base58Type.STEALTH_ADDRESS] = new byte[] { 0x2b };
            this.Base58Prefixes[(int)Base58Type.ASSET_ID] = new byte[] { 115 };
            this.Base58Prefixes[(int)Base58Type.COLORED_ADDRESS] = new byte[] { 0x13 };

            Bech32Encoder encoder = Encoders.Bech32("tb");
            this.Bech32Encoders = new Bech32Encoder[2];
            this.Bech32Encoders[(int)Bech32Type.WITNESS_PUBKEY_ADDRESS] = encoder;
            this.Bech32Encoders[(int)Bech32Type.WITNESS_SCRIPT_ADDRESS] = encoder;

            this.Checkpoints = new Dictionary<int, CheckpointInfo>();

            this.DNSSeeds = new List<DNSSeedData>();

            this.StandardScriptsRegistry = new SmartContractsStandardScriptsRegistry();

            string[] seedNodes = { "40.112.89.58", "137.117.243.54", "51.140.255.152", "40.89.158.103", "40.89.158.153", "13.66.214.36", "23.101.147.254" };
            this.SeedNodes = ConvertToNetworkAddresses(seedNodes, this.DefaultPort).ToList();

            Assert(this.Consensus.HashGenesisBlock == uint256.Parse("0x000004b5e1be2efc806c0e779550e05fa11f4902063f87cc273959fadc5ca579"));
            Assert(this.Genesis.Header.HashMerkleRoot == uint256.Parse("0x55168c43e5b997b99192af9819297efb43bedfdd698f29c6a2c22dfc671cc0fb"));
        }
    }
}