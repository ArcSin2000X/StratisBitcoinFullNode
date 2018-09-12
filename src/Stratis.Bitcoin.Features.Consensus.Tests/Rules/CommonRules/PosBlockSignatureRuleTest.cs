﻿using System.Collections.Generic;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Crypto;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class PosBlockSignatureRuleTest : TestPosConsensusRulesUnitTestBase
    {
        private Key key;

        public PosBlockSignatureRuleTest()
        {
            this.key = new Key();
        }

        [Fact]
        public async Task RunAsync_ProofOfWorkBlockSignatureNotEmpty_ThrowsBadBlockSignatureConsensusErrorExceptionAsync()
        {
            this.ruleContext.ValidationContext.Block = KnownNetworks.StratisMain.Consensus.ConsensusFactory.CreateBlock();

            (this.ruleContext.ValidationContext.Block as PosBlock).BlockSignature = new BlockSignature() { Signature = new byte[] { 0x2, 0x3 } };

            Assert.True(BlockStake.IsProofOfWork(this.ruleContext.ValidationContext.Block));

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<PosBlockSignatureRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadBlockSignature, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_ProofOfStakeBlockSignatureEmpty_ThrowsBadBlockSignatureConsensusErrorExceptionAsync()
        {
            this.ruleContext.ValidationContext.Block = KnownNetworks.StratisMain.Consensus.ConsensusFactory.CreateBlock();
            this.ruleContext.ValidationContext.Block.Transactions.Add(KnownNetworks.StratisMain.CreateTransaction());

            Transaction transaction = KnownNetworks.StratisMain.CreateTransaction();
            transaction.Inputs.Add(new TxIn()
            {
                PrevOut = new OutPoint(new uint256(15), 1),
                ScriptSig = new Script()
            });
            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            this.ruleContext.ValidationContext.Block.Transactions.Add(transaction);

            Assert.True(BlockStake.IsProofOfStake(this.ruleContext.ValidationContext.Block));

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<PosBlockSignatureRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadBlockSignature, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_ProofOfStakeBlock_CoinStakePayToPubScriptKeyInvalid_ThrowsBadBlockSignatureConsensusErrorExceptionAsync()
        {
            Block block = KnownNetworks.StratisMain.Consensus.ConsensusFactory.CreateBlock();
            block.Transactions.Add(KnownNetworks.StratisMain.CreateTransaction());

            Transaction transaction = KnownNetworks.StratisMain.CreateTransaction();
            transaction.Inputs.Add(new TxIn()
            {
                PrevOut = new OutPoint(new uint256(15), 1),
                ScriptSig = new Script()
            });

            // use different key with PayToPubKey script
            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            var scriptPubKeyOut = new Script(Op.GetPushOp(new Key().PubKey.ToBytes(true)), OpcodeType.OP_CHECKSIG);
            transaction.Outputs.Add(new TxOut(Money.Zero, scriptPubKeyOut));
            block.Transactions.Add(transaction);

            ECDSASignature signature = this.key.Sign(block.GetHash());
            (block as PosBlock).BlockSignature = new BlockSignature { Signature = signature.ToDER() };

            this.ruleContext.ValidationContext.Block = block;
            Assert.True(BlockStake.IsProofOfStake(this.ruleContext.ValidationContext.Block));

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<PosBlockSignatureRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadBlockSignature, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_ProofOfStakeBlock_NoOpsInScriptPubKey_ThrowsBadBlockSignatureConsensusErrorExceptionAsync()
        {
            Block block = KnownNetworks.StratisMain.Consensus.ConsensusFactory.CreateBlock();
            block.Transactions.Add(KnownNetworks.StratisMain.CreateTransaction());

            Transaction transaction = KnownNetworks.StratisMain.CreateTransaction();
            transaction.Inputs.Add(new TxIn()
            {
                PrevOut = new OutPoint(new uint256(15), 1),
                ScriptSig = new Script()
            });

            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            transaction.Outputs.Add(new TxOut(Money.Zero, new Script()));
            block.Transactions.Add(transaction);

            ECDSASignature signature = this.key.Sign(block.GetHash());
            (block as PosBlock).BlockSignature = new BlockSignature { Signature = signature.ToDER() };

            this.ruleContext.ValidationContext.Block = block;
            Assert.True(BlockStake.IsProofOfStake(this.ruleContext.ValidationContext.Block));

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<PosBlockSignatureRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadBlockSignature, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_ProofOfStakeBlock_FirstOpInScriptPubKeyNotOP_Return_ThrowsBadBlockSignatureConsensusErrorExceptionAsync()
        {
            Block block = KnownNetworks.StratisMain.Consensus.ConsensusFactory.CreateBlock();
            block.Transactions.Add(KnownNetworks.StratisMain.CreateTransaction());

            Transaction transaction = KnownNetworks.StratisMain.CreateTransaction();
            transaction.Inputs.Add(new TxIn()
            {
                PrevOut = new OutPoint(new uint256(15), 1),
                ScriptSig = new Script()
            });

            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            transaction.Outputs.Add(new TxOut(Money.Zero, new Script(new Op() { Code = OpcodeType.OP_CHECKSIG })));
            block.Transactions.Add(transaction);

            ECDSASignature signature = this.key.Sign(block.GetHash());
            (block as PosBlock).BlockSignature = new BlockSignature { Signature = signature.ToDER() };

            this.ruleContext.ValidationContext.Block = block;
            Assert.True(BlockStake.IsProofOfStake(this.ruleContext.ValidationContext.Block));

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<PosBlockSignatureRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadBlockSignature, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_ProofOfStakeBlock_OpCountBelowTwo_ThrowsBadBlockSignatureConsensusErrorExceptionAsync()
        {
            Block block = KnownNetworks.StratisMain.Consensus.ConsensusFactory.CreateBlock();
            block.Transactions.Add(KnownNetworks.StratisMain.CreateTransaction());

            Transaction transaction = KnownNetworks.StratisMain.CreateTransaction();
            transaction.Inputs.Add(new TxIn()
            {
                PrevOut = new OutPoint(new uint256(15), 1),
                ScriptSig = new Script()
            });

            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            transaction.Outputs.Add(new TxOut(Money.Zero, new Script(new Op() { Code = OpcodeType.OP_RETURN })));
            block.Transactions.Add(transaction);

            ECDSASignature signature = this.key.Sign(block.GetHash());
            (block as PosBlock).BlockSignature = new BlockSignature { Signature = signature.ToDER() };

            this.ruleContext.ValidationContext.Block = block;
            Assert.True(BlockStake.IsProofOfStake(this.ruleContext.ValidationContext.Block));

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<PosBlockSignatureRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadBlockSignature, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_ProofOfStakeBlock_ScriptKeyDoesNotPassCompressedUncompresedKeyValidation_ThrowsBadBlockSignatureConsensusErrorExceptionAsync()
        {
            Block block = KnownNetworks.StratisMain.Consensus.ConsensusFactory.CreateBlock();
            block.Transactions.Add(KnownNetworks.StratisMain.CreateTransaction());

            Transaction transaction = KnownNetworks.StratisMain.CreateTransaction();
            transaction.Inputs.Add(new TxIn()
            {
                PrevOut = new OutPoint(new uint256(15), 1),
                ScriptSig = new Script()
            });

            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            transaction.Outputs.Add(new TxOut(Money.Zero, new Script(new Op() { Code = OpcodeType.OP_RETURN }, new Op() { PushData = new byte[] { 0x11 } })));
            block.Transactions.Add(transaction);

            ECDSASignature signature = this.key.Sign(block.GetHash());
            (block as PosBlock).BlockSignature = new BlockSignature { Signature = signature.ToDER() };

            this.ruleContext.ValidationContext.Block = block;
            Assert.True(BlockStake.IsProofOfStake(this.ruleContext.ValidationContext.Block));

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<PosBlockSignatureRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadBlockSignature, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_ProofOfStakeBlock_ScriptKeyDoesNotPassBlockSignatureValidation_ThrowsBadBlockSignatureConsensusErrorExceptionAsync()
        {
            Block block = KnownNetworks.StratisMain.Consensus.ConsensusFactory.CreateBlock();
            block.Transactions.Add(KnownNetworks.StratisMain.CreateTransaction());

            Transaction transaction = KnownNetworks.StratisMain.CreateTransaction();
            transaction.Inputs.Add(new TxIn()
            {
                PrevOut = new OutPoint(new uint256(15), 1),
                ScriptSig = new Script()
            });

            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));

            // push op_return to note external dependancy in front of pay to pubkey script so it does not match pay to pubkey template.
            // use a different key to generate the script so it does not pass validation.
            var scriptPubKeyOut = new Script(OpcodeType.OP_RETURN, Op.GetPushOp(new Key().PubKey.ToBytes(true)), OpcodeType.OP_CHECKSIG);
            transaction.Outputs.Add(new TxOut(Money.Zero, scriptPubKeyOut));
            block.Transactions.Add(transaction);

            ECDSASignature signature = this.key.Sign(block.GetHash());
            (block as PosBlock).BlockSignature = new BlockSignature { Signature = signature.ToDER() };

            this.ruleContext.ValidationContext.Block = block;
            Assert.True(BlockStake.IsProofOfStake(this.ruleContext.ValidationContext.Block));

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<PosBlockSignatureRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadBlockSignature, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_ProofOfStakeBlock_PayToPubKeyScriptPassesBlockSignatureValidation_DoesNotThrowExceptionAsync()
        {
            Block block = KnownNetworks.StratisMain.Consensus.ConsensusFactory.CreateBlock();
            block.Transactions.Add(KnownNetworks.StratisMain.CreateTransaction());

            Transaction transaction = KnownNetworks.StratisMain.CreateTransaction();
            transaction.Inputs.Add(new TxIn()
            {
                PrevOut = new OutPoint(new uint256(15), 1),
                ScriptSig = new Script()
            });

            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            var scriptPubKeyOut = new Script(Op.GetPushOp(this.key.PubKey.ToBytes(true)), OpcodeType.OP_CHECKSIG);
            transaction.Outputs.Add(new TxOut(Money.Zero, scriptPubKeyOut));
            block.Transactions.Add(transaction);

            ECDSASignature signature = this.key.Sign(block.GetHash());
            (block as PosBlock).BlockSignature = new BlockSignature { Signature = signature.ToDER() };

            this.ruleContext.ValidationContext.Block = block;
            Assert.True(BlockStake.IsProofOfStake(this.ruleContext.ValidationContext.Block));

            await this.consensusRules.RegisterRule<PosBlockSignatureRule>().RunAsync(this.ruleContext);
        }

        [Fact]
        public async Task RunAsync_ProofOfWorkBlock_BlockSignatureEmpty_DoesNotThrowExceptionAsync()
        {
            Block block = KnownNetworks.StratisMain.Consensus.ConsensusFactory.CreateBlock();
            block.Transactions.Add(KnownNetworks.StratisMain.CreateTransaction());

            Transaction transaction = KnownNetworks.StratisMain.CreateTransaction();
            transaction.Inputs.Add(new TxIn()
            {
                PrevOut = new OutPoint(uint256.Zero, uint.MaxValue),
                ScriptSig = new Script()
            });

            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            block.Transactions.Add(transaction);
            (block as PosBlock).BlockSignature = new BlockSignature();

            this.ruleContext.ValidationContext.Block = block;
            Assert.True(BlockStake.IsProofOfWork(this.ruleContext.ValidationContext.Block));

            await this.consensusRules.RegisterRule<PosBlockSignatureRule>().RunAsync(this.ruleContext);
        }

        /// <summary>
        /// This helper creates a coin staking block containing a coin staking transaction built according to
        /// the parameters and with valid first input and output. The block signature is created correctly with the
        /// private key corresponding to the public key.
        /// </summary>
        /// <param name="useCompressedKey">Determines whether the second transaction output will include a compressed
        /// (versus uncompressed) public key.</param>
        /// <param name="includeSecondPush">Determines whether the second transaction output will include a small integer
        /// after the public key.</param>
        /// <param name="expectFailure">Determines whether we expect failure (versus success).</param>
        private void ProofOfStakeBlock_CoinStakeTestHelper(bool useCompressedKey, bool includeSecondPush, bool expectFailure)
        {
            Block block = KnownNetworks.StratisMain.Consensus.ConsensusFactory.CreateBlock();

            // Add a dummy coinbase transaction.
            block.Transactions.Add(KnownNetworks.StratisMain.CreateTransaction());

            // Build a coinstake transaction.
            Transaction coinStakeTransaction = KnownNetworks.StratisMain.CreateTransaction();
            coinStakeTransaction.Inputs.Add(new TxIn()
            {
                PrevOut = new OutPoint(new uint256(15), 1),
                ScriptSig = new Script()
            });

            // First output of coinstake transaction is a special marker.
            coinStakeTransaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));

            // Second (unspendable) output.
            // Depending on the test case use either a compressed public key or an uncompressed public key.
            var pubKey = useCompressedKey ? this.key.PubKey.Compress() : this.key.PubKey.Decompress();
            var opCodes = new List<Op> { OpcodeType.OP_RETURN, Op.GetPushOp(pubKey.ToBytes(true)) };

            // Depending on the test case add a second push of some small integer.
            if (includeSecondPush)
                opCodes.Add(Op.GetPushOp(new byte[] { 123 }));
            coinStakeTransaction.Outputs.Add(new TxOut(Money.Zero, new Script(opCodes)));

            // Add the coinstake transaction.
            block.Transactions.Add(coinStakeTransaction);

            // Add a signature to the block.
            ECDSASignature signature = this.key.Sign(block.GetHash());
            (block as PosBlock).BlockSignature = new BlockSignature { Signature = signature.ToDER() };

            // Execute the PosBlockSignatureRule.
            this.ruleContext.ValidationContext.Block = block;
            Assert.True(BlockStake.IsProofOfStake(this.ruleContext.ValidationContext.Block));

            if (expectFailure)
            {
                ConsensusErrorException exception = Assert.Throws<ConsensusErrorException>(() =>
                    this.consensusRules.RegisterRule<PosBlockSignatureRule>().RunAsync(this.ruleContext).GetAwaiter().GetResult());
                Assert.Equal(ConsensusErrors.BadBlockSignature, exception.ConsensusError);
                return;
            }

            this.consensusRules.RegisterRule<PosBlockSignatureRule>().RunAsync(this.ruleContext).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Given a coinstake transaction with a second output consisting of OP_RETURN followed by a compressed public key
        /// with the block signature correctly created with the corresponding private key expect success.
        /// </summary>
        [Fact]
        public async Task ProofOfStakeBlock_ValidCoinStakeBlockDoesNotThrowExceptionAsync()
        {
            ProofOfStakeBlock_CoinStakeTestHelper(useCompressedKey: true, includeSecondPush: false, expectFailure: false);
        }

        /// <summary>
        /// Given a coinstake transaction with a second output consisting of OP_RETURN followed by a uncompressed public key
        /// with the block signature correctly created with the corresponding private key expect failure.
        /// </summary>
        [Fact]
        public async Task ProofOfStakeBlock_ValidCoinStakeBlockExceptUncompressedKeyThrowsExceptionAsync()
        {
            ProofOfStakeBlock_CoinStakeTestHelper(useCompressedKey: false, includeSecondPush: false, expectFailure: true);
        }

        /// <summary>
        /// Given a coinstake transaction with a second output consisting of OP_RETURN followed by a compressed public key
        /// and a small integer value but with the block signature correctly created with the corresponding private key
        /// expect failure.
        /// </summary>
        [Fact]
        public async Task ProofOfStakeBlock_ValidCoinStakeBlockExceptExtraPushThrowsExceptionAsync()
        {
            ProofOfStakeBlock_CoinStakeTestHelper(useCompressedKey: true, includeSecondPush: true, expectFailure: true);
        }
    }
}
