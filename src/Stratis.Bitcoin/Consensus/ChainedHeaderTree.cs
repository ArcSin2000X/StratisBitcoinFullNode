﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Consensus.Validators;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Consensus
{
    /// <summary>
    /// Tree of chained block headers that are being claimed by the connected peers and the node itself.
    /// It represents all chains we potentially can sync with.
    /// </summary>
    /// <remarks>
    /// This component is an extension of <see cref="ConsensusManager"/> and is strongly linked to its functionality, it should never be called outside of CM.
    /// <para>
    /// View of the chains that are presented by connected peers might be incomplete because we always
    /// receive only chunk of headers claimed by the peer in one message.
    /// </para>
    /// <para>
    /// It is a role of the consensus manager to decide which of the presented chains is going to be treated as our best chain.
    /// <see cref="ChainedHeaderTree"/> only advices which chains it might be interesting to download.
    /// </para>
    /// <para>
    /// This class is not thread safe and it the role of the component that uses this class to prevent race conditions.
    /// </para>
    /// </remarks>
    public interface IChainedHeaderTree
    {
        /// <summary>
        /// Total size of unconsumed blocks data in bytes.
        /// It represents amount of memory which is occupied by block data that is waiting to be processed.
        /// </summary>
        /// <remarks>
        /// This value is increased every time a new block is downloaded.
        /// It's decreased when block header is being disconnected or when consensus tip is changed.
        /// </remarks>
        long UnconsumedBlocksDataBytes { get; }

        /// <summary>
        /// Initialize the tree with consensus tip.
        /// </summary>
        /// <param name="consensusTip">The consensus tip.</param>
        /// <param name="blockStoreAvailable">Specifies if block store is enabled.</param>
        /// <exception cref="ConsensusException">Thrown in case given <paramref name="consensusTip"/> is on a wrong network.</exception>
        void Initialize(ChainedHeader consensusTip, bool blockStoreAvailable);

        /// <summary>
        /// Remove a peer and the entire branch of the tree that it claims unless the
        /// headers are part of our consensus chain or are claimed by other peers.
        /// </summary>
        /// <param name="networkPeerId">Id of a peer that was disconnected.</param>
        void PeerDisconnected(int networkPeerId);

        /// <summary>
        /// Mark a <see cref="ChainedHeader"/> as <see cref="ValidationState.FullyValidated"/>.
        /// </summary>
        /// <param name="chainedHeader">The fully validated header.</param>
        void FullValidationSucceeded(ChainedHeader chainedHeader);

        /// <summary>
        /// Handles situation when partial validation for block data for a given <see cref="ChainedHeader"/> was successful.
        /// </summary>
        /// <remarks>
        /// In case partial validation was successful we want to partially validate all the next blocks for which we have block data for.
        /// <para>
        /// If block that was just partially validated has more cumulative chainwork than our consensus tip we want to switch our consensus tip to this block.
        /// </para>
        /// </remarks>
        /// <param name="chainedHeader">The chained header.</param>
        /// <param name="fullValidationRequired"><c>true</c> in case we want to switch our consensus tip to <paramref name="chainedHeader"/>.</param>
        /// <returns>List of chained header blocks with block data that should be partially validated next. Or <c>null</c> if none should be validated.</returns>
        List<ChainedHeaderBlock> PartialValidationSucceeded(ChainedHeader chainedHeader, out bool fullValidationRequired);

        /// <summary>
        /// Handles situation when block data was considered to be invalid
        /// for a given header during the partial or full validation.
        /// </summary>
        /// <param name="chainedHeader">Chained header which block data failed the validation.</param>
        /// <returns>List of peer Ids that were claiming chain that contains an invalid block. Such peers should be banned.</returns>
        List<int> PartialOrFullValidationFailed(ChainedHeader chainedHeader);

        /// <summary>
        /// Handles situation when consensuses tip was changed.
        /// </summary>
        /// <remarks>
        /// All peers are checked against max reorg violation and if they violate their chain will be reset.
        /// </remarks>
        /// <param name="newConsensusTip">The new consensus tip.</param>
        /// <returns>List of peer Ids that violate max reorg rule.</returns>
        List<int> ConsensusTipChanged(ChainedHeader newConsensusTip);

        /// <summary>
        /// Finds the header and verifies block integrity.
        /// </summary>
        /// <param name="block">The block.</param>
        /// <returns>Chained header for a given block.</returns>
        /// <exception cref="BlockDownloadedForMissingChainedHeaderException">Thrown when block data is presented for a chained block that doesn't exist.</exception>
        ChainedHeader FindHeaderAndVerifyBlockIntegrity(Block block);

        /// <summary>
        /// Handles situation when the blocks data is downloaded for a given chained header.
        /// </summary>
        /// <param name="chainedHeader">Chained header that represents <paramref name="block"/>.</param>
        /// <param name="block">Block data.</param>
        /// <returns><c>true</c> in case partial validation is required for the downloaded block, <c>false</c> otherwise.</returns>
        bool BlockDataDownloaded(ChainedHeader chainedHeader, Block block);

        /// <summary>
        /// A new list of headers are presented by a peer, the headers will try to be connected to the tree.
        /// Blocks associated with headers that are interesting (i.e. represent a chain with greater chainwork than our consensus tip)
        /// will be requested for download.
        /// </summary>
        /// <remarks>
        /// The headers are assumed to be in consecutive order.
        /// </remarks>
        /// <param name="networkPeerId">Id of a peer that presented the headers.</param>
        /// <param name="headers">The list of headers to connect to the tree.</param>
        /// <returns>
        /// Information about which blocks need to be downloaded together with information about which input headers were processed.
        /// Only headers that we can validate will be processed. The rest of the headers will be submitted later again for processing.
        /// </returns>
        /// <exception cref="ConnectHeaderException">Thrown when first presented header can't be connected to any known chain in the tree.</exception>
        /// <exception cref="CheckpointMismatchException">Thrown if checkpointed header doesn't match the checkpoint hash.</exception>
        ConnectNewHeadersResult ConnectNewHeaders(int networkPeerId, List<BlockHeader> headers);

        /// <summary>
        /// Creates the chained header for a new block.
        /// </summary>
        /// <param name="block">The block.</param>
        /// <returns>Newly created and connected chained header for the specified block.</returns>
        ChainedHeader CreateChainedHeaderWithBlock(Block block);

        /// <summary>
        /// Get the block and its chained header if it exists.
        /// If the header is not in the tree <see cref="ChainedHeaderBlock"/> will be <c>null</c>, the <see cref="ChainedHeaderBlock.Block"/> may also be null.
        /// </summary>
        /// <remarks>
        /// The block can be <c>null</c> when the block data has not yet been downloaded or if the block data has been persisted to the database and removed from the memory.
        /// </remarks>
        /// <returns>The block and its chained header (the <see cref="ChainedHeaderBlock.Block"/> can be <c>null</c> or the <see cref="ChainedHeaderBlock"/> result can be <c>null</c>).</returns>
        ChainedHeaderBlock GetChainedHeaderBlock(uint256 blockHash);

        /// <summary>Get the chained header.</summary>
        /// <returns>Chained header for specified block hash if it exists, <c>null</c> otherwise.</returns>
        ChainedHeader GetChainedHeader(uint256 blockHash);
    }

    /// <inheritdoc />
    internal sealed class ChainedHeaderTree : IChainedHeaderTree
    {
        private readonly Network network;
        private readonly IHeaderValidator headerValidator;
        private readonly IIntegrityValidator integrityValidator;
        private readonly ILogger logger;
        private readonly ICheckpoints checkpoints;
        private readonly IChainState chainState;
        private readonly ConsensusSettings consensusSettings;
        private readonly ISignals signals;
        private readonly IFinalizedBlockHeight finalizedBlockHeight;

        /// <inheritdoc />
        public long UnconsumedBlocksDataBytes { get; private set; }

        /// <summary>A special peer identifier that represents our local node.</summary>
        internal const int LocalPeerId = -1;

        /// <summary>Specifies for how many blocks from the consensus tip the block data should be kept in the memory.</summary>
        /// <remarks>
        /// TODO: calculate the actual value based on the max block size. Set threshold in bytes. Make it configurable.
        /// </remarks>
        internal const int KeepBlockDataForLastBlocks = 100;

        /// <summary>Lists of peer identifiers mapped by hashes of the block headers that are considered to be their tips.</summary>
        /// <remarks>
        /// During the consensus tip changing process, which includes both the reorganization and advancement on the same chain,
        /// it happens that there are two entries for <see cref="LocalPeerId"/>. This means that two different blocks are being
        /// claimed by our node as its tip. This is necessary in order to protect the new consensus tip candidate from being
        /// removed in case peers that were claiming it disconnect during the consensus tip changing process.
        /// <para>
        /// All the leafs of the tree have to be tips of chains presented by peers, which means that
        /// hashes of the leaf block headers have to be keys with non-empty values in this dictionary.
        /// </para>
        /// </remarks>
        private readonly Dictionary<uint256, HashSet<int>> peerIdsByTipHash;

        /// <summary>A list of peer identifiers that are mapped to their tips.</summary>
        private readonly Dictionary<int, uint256> peerTipsByPeerId;

        /// <summary>
        /// Chained headers mapped by their hashes.
        /// Every chained header that is connected to the tree has to have its hash in this dictionary.
        /// </summary>
        private readonly Dictionary<uint256, ChainedHeader> chainedHeadersByHash;

        /// <summary><c>true</c> if block store feature is enabled.</summary>
        private bool blockStoreAvailable;

        public ChainedHeaderTree(
            Network network,
            ILoggerFactory loggerFactory,
            IHeaderValidator headerValidator,
            IIntegrityValidator integrityValidator,
            ICheckpoints checkpoints,
            IChainState chainState,
            IFinalizedBlockHeight finalizedBlockHeight,
            ConsensusSettings consensusSettings,
            ISignals signals)
        {
            this.network = network;
            this.headerValidator = headerValidator;
            this.integrityValidator = integrityValidator;
            this.checkpoints = checkpoints;
            this.chainState = chainState;
            this.finalizedBlockHeight = finalizedBlockHeight;
            this.consensusSettings = consensusSettings;
            this.signals = signals;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            this.peerTipsByPeerId = new Dictionary<int, uint256>();
            this.peerIdsByTipHash = new Dictionary<uint256, HashSet<int>>();
            this.chainedHeadersByHash = new Dictionary<uint256, ChainedHeader>();
            this.UnconsumedBlocksDataBytes = 0;
        }

        /// <inheritdoc />
        public void Initialize(ChainedHeader consensusTip, bool blockStoreAvailable)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:{3})", nameof(consensusTip), consensusTip, nameof(blockStoreAvailable), blockStoreAvailable);

            ChainedHeader current = consensusTip;
            this.blockStoreAvailable = blockStoreAvailable;

            while (current.Previous != null)
            {
                current.Previous.Next.Add(current);
                this.chainedHeadersByHash.Add(current.HashBlock, current);

                current.BlockDataAvailability = blockStoreAvailable ? BlockDataAvailabilityState.BlockAvailable : BlockDataAvailabilityState.HeaderOnly;
                current.BlockValidationState = ValidationState.FullyValidated;

                current = current.Previous;
            }

            // Add the genesis block.
            this.chainedHeadersByHash.Add(current.HashBlock, current);

            if (current.HashBlock != this.network.GenesisHash)
            {
                this.logger.LogTrace("(-)[INVALID_NETWORK]");
                throw new ConsensusException("INVALID_NETWORK");
            }

            // Initialize local tip claim with consensus tip.
            this.AddOrReplacePeerTip(LocalPeerId, consensusTip.HashBlock);

            this.logger.LogTrace("(-)");
        }

        // <inheritdoc />
        public ChainedHeaderBlock GetChainedHeaderBlock(uint256 blockHash)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(blockHash), blockHash);

            ChainedHeaderBlock chainedHeaderBlock = null;
            if (this.chainedHeadersByHash.TryGetValue(blockHash, out ChainedHeader chainedHeader))
            {
                chainedHeaderBlock = new ChainedHeaderBlock(chainedHeader.Block, chainedHeader);
            }

            this.logger.LogTrace("(-):'{0}'", chainedHeaderBlock);
            return chainedHeaderBlock;
        }

        // <inheritdoc />
        public ChainedHeader GetChainedHeader(uint256 blockHash)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(blockHash), blockHash);

            if (this.chainedHeadersByHash.TryGetValue(blockHash, out ChainedHeader chainedHeader))
            {
                this.logger.LogTrace("(-):'{0}'", chainedHeader);
                return chainedHeader;
            }

            this.logger.LogTrace("(-):null");
            return null;
        }

        /// <summary>Gets the consensus tip.</summary>
        private ChainedHeader GetConsensusTip()
        {
            uint256 consensusTipHash = this.peerTipsByPeerId[LocalPeerId];
            return this.chainedHeadersByHash[consensusTipHash];
        }

        // <inheritdoc />
        public void PeerDisconnected(int networkPeerId)
        {
            this.logger.LogTrace("({0}:{1})", nameof(networkPeerId), networkPeerId);

            if (!this.peerTipsByPeerId.TryGetValue(networkPeerId, out uint256 peerTipHash))
            {
                this.logger.LogTrace("(-)[PEER_TIP_NOT_FOUND]");
                return;
            }

            ChainedHeader peerTip;
            if (!this.chainedHeadersByHash.TryGetValue(peerTipHash, out peerTip))
            {
                this.logger.LogError("Header '{0}' not found but it is claimed by {1} as its tip.", peerTipHash, networkPeerId);
                this.logger.LogTrace("(-)[HEADER_NOT_FOUND]");
                throw new ConsensusException("Header not found!");
            }

            this.RemovePeerClaim(networkPeerId, peerTip);

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public void FullValidationSucceeded(ChainedHeader chainedHeader)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(chainedHeader), chainedHeader);

            chainedHeader.BlockValidationState = ValidationState.FullyValidated;

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public List<ChainedHeaderBlock> PartialValidationSucceeded(ChainedHeader chainedHeader, out bool fullValidationRequired)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(chainedHeader), chainedHeader);

            fullValidationRequired = false;

            // Can happen in case peer was disconnected during the validation and it was the only peer claiming that header.
            if (!this.chainedHeadersByHash.ContainsKey(chainedHeader.HashBlock))
            {
                this.logger.LogTrace("(-)[HEADER_NOT_FOUND]:null");
                return null;
            }

            // Can happen when peer was disconnected after sending the block but before the validation was completed
            // and right after that a new peer connected and presented the same header.
            if (chainedHeader.Block == null)
            {
                this.logger.LogTrace("(-)[BLOCK_DATA_NULL]:null");
                return null;
            }

            // Can happen in case of a race condition when peer 1 presented a block, we started partial validation, peer 1 disconnected,
            // peer 2 connected, presented header and supplied a block and block puller pushed it so the block data is not null.
            if ((chainedHeader.Previous.BlockValidationState != ValidationState.PartiallyValidated) &&
                (chainedHeader.Previous.BlockValidationState != ValidationState.FullyValidated))
            {
                this.logger.LogTrace("(-)[PREV_BLOCK_NOT_VALIDATED]:null");
                return null;
            }

            // Same scenario as above except for prev block was validated which triggered next partial validation to be started.
            if ((chainedHeader.BlockValidationState == ValidationState.PartiallyValidated) ||
                (chainedHeader.BlockValidationState == ValidationState.FullyValidated))
            {
                this.logger.LogTrace("(-)[ALREADY_VALIDATED]:null");
                return null;
            }

            chainedHeader.BlockValidationState = ValidationState.PartiallyValidated;

            if (chainedHeader.ChainWork > this.GetConsensusTip().ChainWork)
            {
                // A better tip that was partially validated is found. Set our node's claim on the tip to avoid that chain removal in
                // case all the peers that were claiming a better chain are disconnected before we switch consensus tip of our node.
                // This is a special case when we have our node claiming two tips at the same time - the previous consensus tip and the one
                // that we are trying to switch to.
                this.logger.LogDebug("Partially validated chained header '{0}' has more work than the current consensus tip.", chainedHeader);

                this.ClaimPeerTip(LocalPeerId, chainedHeader.HashBlock);

                fullValidationRequired = true;
            }

            var chainedHeaderBlocksToValidate = new List<ChainedHeaderBlock>();
            foreach (ChainedHeader header in chainedHeader.Next)
            {
                if (header.BlockDataAvailability == BlockDataAvailabilityState.BlockAvailable)
                {
                    // Block header is not ancestor of the consensus tip so it's block data is guaranteed to be there.
                    chainedHeaderBlocksToValidate.Add(new ChainedHeaderBlock(header.Block, header));
                    this.logger.LogTrace("Chained header '{0}' is selected for partial validation.", header);
                }
            }

            this.logger.LogTrace("(-):*.{0}={1},{2}={3}", nameof(chainedHeaderBlocksToValidate.Count), chainedHeaderBlocksToValidate.Count, nameof(fullValidationRequired), fullValidationRequired);
            return chainedHeaderBlocksToValidate;
        }

        /// <summary>Sets the tip claim for a peer.</summary>
        /// <param name="networkPeerId">Peer Id.</param>
        /// <param name="tipHash">Tip's hash.</param>
        private void ClaimPeerTip(int networkPeerId, uint256 tipHash)
        {
            this.logger.LogTrace("({0}:{1},{2}:'{3}')", nameof(networkPeerId), networkPeerId, nameof(tipHash), tipHash);

            HashSet<int> peersClaimingThisHeader;
            if (!this.peerIdsByTipHash.TryGetValue(tipHash, out peersClaimingThisHeader))
            {
                peersClaimingThisHeader = new HashSet<int>();
                this.peerIdsByTipHash.Add(tipHash, peersClaimingThisHeader);
            }

            peersClaimingThisHeader.Add(networkPeerId);

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public List<int> PartialOrFullValidationFailed(ChainedHeader chainedHeader)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(chainedHeader), chainedHeader);

            // Can happen in case peer was disconnected during the validation and it was the only peer claiming that header.
            if (!this.chainedHeadersByHash.ContainsKey(chainedHeader.HashBlock))
            {
                this.logger.LogTrace("(-)[NOT_FOUND]");
                return new List<int>();
            }

            List<int> peersToBan = this.RemoveSubtree(chainedHeader);

            this.RemoveUnclaimedBranch(chainedHeader.Previous);

            this.logger.LogTrace("(-):*.{0}={1}", nameof(peersToBan.Count), peersToBan.Count);
            return peersToBan;
        }

        /// <inheritdoc />
        public List<int> ConsensusTipChanged(ChainedHeader newConsensusTip)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(newConsensusTip), newConsensusTip);

            ChainedHeader oldConsensusTip = this.GetConsensusTip();
            ChainedHeader fork = newConsensusTip.FindFork(oldConsensusTip);
            ChainedHeader currentHeader = newConsensusTip;

            this.logger.LogTrace("Old consensus tip: '{0}', new consensus tip: '{1}', fork point: '{2}'.", oldConsensusTip, newConsensusTip, fork);

            // Consider blocks that became a part of our best chain as consumed.
            while (currentHeader != fork)
            {
                this.UnconsumedBlocksDataBytes -= currentHeader.Block.BlockSize.Value;
                this.logger.LogTrace("Size of unconsumed block data is decreased by {0}, new value is {1}.", currentHeader.Block.BlockSize.Value, this.UnconsumedBlocksDataBytes);
                currentHeader = currentHeader.Previous;
            }

            // When we are switching to a different chain remove block data for blocks that are being reorged away.
            // We are also marking block data availability as header only because we expect block store to reorganize those blocks shortly.
            // In case chain that we reorged away from gets a prolongation which will make it desirable we will need to redownload all those blocks again.
            if (fork != oldConsensusTip)
            {
                this.logger.LogTrace("Consensus tip is being changed to another chain, removing block data for the old chain.");
                currentHeader = oldConsensusTip;

                while (currentHeader != fork)
                {
                    currentHeader.Block = null;
                    currentHeader.BlockDataAvailability = BlockDataAvailabilityState.HeaderOnly;

                    this.logger.LogTrace("Block data for '{0}' is removed.", currentHeader);

                    currentHeader = currentHeader.Previous;
                }
            }

            // Switch consensus tip to the new block header.
            this.AddOrReplacePeerTip(LocalPeerId, newConsensusTip.HashBlock);

            List<int> peerIdsToResync = this.FindPeersToResync(newConsensusTip);

            // Remove block data for the headers that are too far from the consensus tip.
            this.CleanOldBlockDataFromMemory(newConsensusTip);

            this.logger.LogTrace("(-)");
            return peerIdsToResync;
        }

        /// <summary>Checks each peer's tip if it violates max reorg rule.
        /// Peers that violate it must be resynced.</summary>
        /// <param name="consensusTip">Consensus tip.</param>
        /// <returns>List of peers which tips violate max reorg rule.</returns>
        private List<int> FindPeersToResync(ChainedHeader consensusTip)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(consensusTip), consensusTip);

            var peerIdsToResync = new List<int>();
            uint maxReorgLength = this.chainState.MaxReorgLength;

            // Find peers with chains that now violate max reorg.
            if (maxReorgLength != 0)
            {
                foreach (KeyValuePair<int, uint256> peerIdToTipHash in this.peerTipsByPeerId)
                {
                    ChainedHeader peerTip = this.chainedHeadersByHash[peerIdToTipHash.Value];
                    int peerId = peerIdToTipHash.Key;

                    ChainedHeader fork = this.FindForkIfChainedHeadersNotOnSameChain(peerTip, consensusTip);

                    int finalizedHeight = this.finalizedBlockHeight.GetFinalizedBlockHeight();

                    // Do nothing in case peer's tip is on our consensus chain.
                    if ((fork != null) && (fork.Height < finalizedHeight))
                    {
                        peerIdsToResync.Add(peerId);
                        this.logger.LogTrace("Peer with Id {0} claims a chain that violates max reorg, its tip is '{1}' and the last finalized block height is {2}.", peerId, peerTip, finalizedHeight);
                    }
                }
            }

            this.logger.LogTrace("(-):*.{0}={1}", nameof(peerIdsToResync.Count), peerIdsToResync.Count);
            return peerIdsToResync;
        }

        /// <summary>Find the fork between two headers and return the fork if the headers are not on the same chain.</summary>
        private ChainedHeader FindForkIfChainedHeadersNotOnSameChain(ChainedHeader chainedHeader1, ChainedHeader chainedHeader2)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}')", nameof(chainedHeader1), chainedHeader1, nameof(chainedHeader2), chainedHeader2);

            ChainedHeader fork = chainedHeader1.FindFork(chainedHeader2);

            if ((fork != chainedHeader2) && (fork != chainedHeader1))
            {
                this.logger.LogTrace("(-):'{0}'", fork);
                return fork;
            }

            this.logger.LogTrace("(-):null");
            return null;
        }

        /// <summary>Cleans the block data for chained headers that are old. This data will still exist in the block store if it is enabled.</summary>
        /// <param name="consensusTip">Consensus tip.</param>
        private void CleanOldBlockDataFromMemory(ChainedHeader consensusTip)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(consensusTip), consensusTip);

            int lastBlockHeightToKeep = consensusTip.Height - KeepBlockDataForLastBlocks;

            if (lastBlockHeightToKeep <= 0)
            {
                this.logger.LogTrace("(-)[GENESIS_REACHED]");
                return;
            }

            ChainedHeader currentBlockToDeleteData = consensusTip.GetAncestor(lastBlockHeightToKeep).Previous;

            // Process blocks that were not process before or until the genesis block exclusive.
            while ((currentBlockToDeleteData.Block != null) && (currentBlockToDeleteData.Previous != null))
            {
                currentBlockToDeleteData.Block = null;
                if (!this.blockStoreAvailable)
                    currentBlockToDeleteData.BlockDataAvailability = BlockDataAvailabilityState.HeaderOnly;

                this.logger.LogTrace("Block data for '{0}' was removed from memory, block data availability is {1}.", currentBlockToDeleteData, currentBlockToDeleteData.BlockDataAvailability);

                currentBlockToDeleteData = currentBlockToDeleteData.Previous;
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Remove all the branches in the tree that are after the given <paramref name="subtreeRoot"/>
        /// including it and return all the peers that where claiming next headers.
        /// </summary>
        /// <param name="subtreeRoot">The chained header to start from.</param>
        /// <returns>List of peer Ids that were claiming headers on removed chains. Such peers should be banned.</returns>
        private List<int> RemoveSubtree(ChainedHeader subtreeRoot)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(subtreeRoot), subtreeRoot);

            var peersToBan = new List<int>();

            var headersToProcess = new Stack<ChainedHeader>();
            headersToProcess.Push(subtreeRoot);

            while (headersToProcess.Count != 0)
            {
                ChainedHeader header = headersToProcess.Pop();

                foreach (ChainedHeader nextHeader in header.Next)
                    headersToProcess.Push(nextHeader);

                if (this.peerIdsByTipHash.TryGetValue(header.HashBlock, out HashSet<int> peers))
                {
                    foreach (int peerId in peers)
                    {
                        // There was a partially validated chain that was better than our consensus tip, we've started full validation
                        // and found out that a block on this chain is invalid. At this point we have a marker with LocalPeerId on the new chain
                        // but our consensus tip inside peerTipsByPeerId has not been changed yet, therefore we want to prevent removing
                        // the consensus tip from the structure.
                        if (peerId != LocalPeerId)
                        {
                            this.peerTipsByPeerId.Remove(peerId);
                            peersToBan.Add(peerId);
                        }
                    }

                    this.peerIdsByTipHash.Remove(header.HashBlock);
                }

                this.DisconnectChainHeader(header);
            }

            this.logger.LogTrace("(-)");
            return peersToBan;
        }

        private void DisconnectChainHeader(ChainedHeader header)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(header), header);

            header.Previous.Next.Remove(header);
            this.chainedHeadersByHash.Remove(header.HashBlock);

            if (header.Block != null)
            {
                this.UnconsumedBlocksDataBytes -= header.Block.BlockSize.Value;
                this.logger.LogTrace("Size of unconsumed block data is decreased by {0}, new value is {1}.", header.Block.BlockSize.Value, this.UnconsumedBlocksDataBytes);
            }

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public ChainedHeader FindHeaderAndVerifyBlockIntegrity(Block block)
        {
            uint256 blockHash = block.GetHash();
            this.logger.LogTrace("({0}:'{1}')", nameof(block), blockHash);

            ChainedHeader chainedHeader;

            if (!this.chainedHeadersByHash.TryGetValue(blockHash, out chainedHeader))
            {
                this.logger.LogTrace("(-)[HEADER_NOT_FOUND]");
                throw new BlockDownloadedForMissingChainedHeaderException();
            }

            this.integrityValidator.VerifyBlockIntegrity(block, chainedHeader);

            this.logger.LogTrace("(-):'{0}'", chainedHeader);
            return chainedHeader;
        }

        /// <inheritdoc />
        public bool BlockDataDownloaded(ChainedHeader chainedHeader, Block block)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(chainedHeader), chainedHeader);

            if (chainedHeader.BlockValidationState == ValidationState.FullyValidated)
            {
                this.logger.LogTrace("(-)[HEADER_FULLY_VALIDATED]:false");
                return false;
            }

            chainedHeader.BlockDataAvailability = BlockDataAvailabilityState.BlockAvailable;
            chainedHeader.Block = block;

            this.UnconsumedBlocksDataBytes += chainedHeader.Block.BlockSize.Value;
            this.logger.LogTrace("Size of unconsumed block data is increased by {0}, new value is {1}.", chainedHeader.Block.BlockSize.Value, this.UnconsumedBlocksDataBytes);

            bool partialValidationRequired = chainedHeader.Previous.BlockValidationState == ValidationState.PartiallyValidated
                                          || chainedHeader.Previous.BlockValidationState == ValidationState.FullyValidated;

            this.logger.LogTrace("(-):{0},{1}='{2}'", partialValidationRequired, nameof(chainedHeader), chainedHeader);
            return partialValidationRequired;
        }

        /// <inheritdoc />
        public ConnectNewHeadersResult ConnectNewHeaders(int networkPeerId, List<BlockHeader> headers)
        {
            Guard.NotNull(headers, nameof(headers));
            this.logger.LogTrace("({0}:{1},{2}.{3}:{4})", nameof(networkPeerId), networkPeerId, nameof(headers), nameof(headers.Count), headers.Count);

            if (!this.chainedHeadersByHash.ContainsKey(headers[0].HashPrevBlock))
            {
                this.logger.LogTrace("(-)[HEADER_COULD_NOT_CONNECT]");
                throw new ConnectHeaderException();
            }

            uint256 lastHash = headers.Last().GetHash();

            List<ChainedHeader> newChainedHeaders = null;

            if (!this.chainedHeadersByHash.ContainsKey(lastHash))
                newChainedHeaders = this.CreateNewHeaders(headers);
            else
                this.logger.LogTrace("No new headers presented.");

            this.AddOrReplacePeerTip(networkPeerId, lastHash);

            if (newChainedHeaders == null)
            {
                this.logger.LogTrace("(-)[NO_NEW_HEADERS]");
                return new ConnectNewHeadersResult() { Consumed = this.chainedHeadersByHash[lastHash] };
            }

            ChainedHeader earliestNewHeader = newChainedHeaders.First();
            ChainedHeader latestNewHeader = newChainedHeaders.Last();

            ConnectNewHeadersResult connectNewHeadersResult = null;

            bool isAssumedValidEnabled = this.consensusSettings.BlockAssumedValid != null;
            bool isBelowLastCheckpoint = this.consensusSettings.UseCheckpoints && (earliestNewHeader.Height <= this.checkpoints.GetLastCheckpointHeight());

            if (isBelowLastCheckpoint || isAssumedValidEnabled)
            {
                ChainedHeader currentChainedHeader = latestNewHeader;

                // When we look for a checkpoint header or an assume valid header, we go from the last presented
                // header to the first one in the reverse order because if there were multiple checkpoints or a checkpoint
                // and an assume valid header inside of the presented list of headers, we would only be interested in the last
                // one as it would cover all previous headers. Reversing the order of processing guarantees that we only need
                // to deal with one special header, which simplifies the implementation.
                while (currentChainedHeader != earliestNewHeader.Previous)
                {
                    if (currentChainedHeader.HashBlock == this.consensusSettings.BlockAssumedValid)
                    {
                        this.logger.LogDebug("Chained header '{0}' represents an assumed valid block.", currentChainedHeader);

                        bool assumeValidBelowLastCheckpoint = this.consensusSettings.UseCheckpoints && (currentChainedHeader.Height <= this.checkpoints.GetLastCheckpointHeight());
                        connectNewHeadersResult = this.HandleAssumedValidHeader(currentChainedHeader, latestNewHeader, assumeValidBelowLastCheckpoint);
                        break;
                    }

                    CheckpointInfo checkpoint = this.checkpoints.GetCheckpoint(currentChainedHeader.Height);
                    if (checkpoint != null)
                    {
                        this.logger.LogDebug("Chained header '{0}' is a checkpoint.", currentChainedHeader);

                        connectNewHeadersResult = this.HandleCheckpointsHeader(currentChainedHeader, latestNewHeader, checkpoint, networkPeerId);
                        break;
                    }

                    currentChainedHeader = currentChainedHeader.Previous;
                }

                if ((connectNewHeadersResult == null) && isBelowLastCheckpoint)
                {
                    connectNewHeadersResult = new ConnectNewHeadersResult() {Consumed = latestNewHeader};
                    this.logger.LogTrace("Chained header '{0}' below last checkpoint.", currentChainedHeader);
                }

                if (connectNewHeadersResult != null)
                {
                    this.logger.LogTrace("(-)[CHECKPOINT_OR_ASSUMED_VALID]:{0}", connectNewHeadersResult);
                    return connectNewHeadersResult;
                }
            }

            if (latestNewHeader.ChainWork > this.GetConsensusTip().ChainWork)
            {
                this.logger.LogDebug("Chained header '{0}' is the tip of a chain with more work than our current consensus tip.", latestNewHeader);

                connectNewHeadersResult = this.MarkBetterChainAsRequired(latestNewHeader, latestNewHeader);
            }

            if (connectNewHeadersResult == null)
                connectNewHeadersResult = new ConnectNewHeadersResult() { Consumed = latestNewHeader };

            this.logger.LogTrace("(-):{0}", connectNewHeadersResult);
            return connectNewHeadersResult;
        }

        /// <summary>
        /// A chain with more work than our current consensus tip was found so mark all it's descendants as required.
        /// </summary>
        /// <param name="lastRequiredHeader">Last header that should be required for download.</param>
        /// <param name="lastNewHeader">Last new header that was created.</param>
        /// <returns>The new headers that need to be downloaded.</returns>
        private ConnectNewHeadersResult MarkBetterChainAsRequired(ChainedHeader lastRequiredHeader, ChainedHeader lastNewHeader)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}')", nameof(lastRequiredHeader), lastRequiredHeader, nameof(lastNewHeader), lastNewHeader);

            var connectNewHeadersResult = new ConnectNewHeadersResult();
            connectNewHeadersResult.DownloadTo = lastRequiredHeader;

            connectNewHeadersResult.Consumed = lastNewHeader;

            ChainedHeader current = lastRequiredHeader;
            ChainedHeader next = current;

            while (!this.HeaderWasRequested(current))
            {
                current.BlockDataAvailability = BlockDataAvailabilityState.BlockRequired;

                next = current;
                current = current.Previous;
            }

            connectNewHeadersResult.DownloadFrom = next;

            this.logger.LogTrace("(-):{0}", connectNewHeadersResult);
            return connectNewHeadersResult;
        }

        /// <summary>
        /// Mark the chain ending with <paramref name="chainedHeader"/> as <see cref="ValidationState.AssumedValid"/>.
        /// </summary>
        /// <param name="chainedHeader">Last <see cref="ChainedHeader"/> to be marked <see cref="ValidationState.AssumedValid"/>.</param>
        private void MarkTrustedChainAsAssumedValid(ChainedHeader chainedHeader)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(chainedHeader), chainedHeader);

            ChainedHeader current = chainedHeader;

            while (!this.HeaderWasMarkedAsValidated(current))
            {
                current.BlockValidationState = ValidationState.AssumedValid;
                current = current.Previous;
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// The header is assumed to be valid, the header and all of its previous headers will be marked as <see cref="ValidationState.AssumedValid" />.
        /// If the header's cumulative work is better then <see cref="IChainState.ConsensusTip" /> the header and all its predecessors will be marked with <see cref="BlockDataAvailabilityState.BlockRequired" />.
        /// </summary>
        /// <param name="assumedValidHeader">The header that is assumed to be valid.</param>
        /// <param name="latestNewHeader">The last header in the list of presented new headers.</param>
        /// <param name="isBelowLastCheckpoint">Set to <c>true</c> if <paramref name="assumedValidHeader"/> is below the last checkpoint,
        /// <c>false</c> otherwise or if checkpoints are disabled.</param>
        private ConnectNewHeadersResult HandleAssumedValidHeader(ChainedHeader assumedValidHeader, ChainedHeader latestNewHeader, bool isBelowLastCheckpoint)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}',{4}:{5})", nameof(assumedValidHeader), assumedValidHeader, nameof(latestNewHeader), latestNewHeader, nameof(isBelowLastCheckpoint), isBelowLastCheckpoint);

            ChainedHeader bestTip = this.GetConsensusTip();
            var connectNewHeadersResult = new ConnectNewHeadersResult() {Consumed = latestNewHeader};

            if (latestNewHeader.ChainWork > bestTip.ChainWork)
            {
                this.logger.LogDebug("Chained header '{0}' is the tip of a chain with more work than our current consensus tip.", latestNewHeader);

                ChainedHeader latestHeaderToMark = isBelowLastCheckpoint ? assumedValidHeader : latestNewHeader;
                connectNewHeadersResult = this.MarkBetterChainAsRequired(latestHeaderToMark, latestNewHeader);
            }

            this.MarkTrustedChainAsAssumedValid(assumedValidHeader);

            this.logger.LogTrace("(-):{0}", connectNewHeadersResult);
            return connectNewHeadersResult;
        }

        /// <summary>
        /// When a header is checkpointed and has a correct hash, chain that ends with such a header
        /// will be marked as <see cref="ValidationState.AssumedValid" /> and requested for download.
        /// </summary>
        /// <param name="chainedHeader">Checkpointed header.</param>
        /// <param name="latestNewHeader">The latest new header that was presented by the peer.</param>
        /// <param name="checkpoint">Information about the checkpoint at the height of the <paramref name="chainedHeader" />.</param>
        /// <param name="peerId">Peer Id that presented chain which contains checkpointed header.</param>
        /// <exception cref="CheckpointMismatchException">Thrown if checkpointed header doesn't match the checkpoint hash.</exception>
        private ConnectNewHeadersResult HandleCheckpointsHeader(ChainedHeader chainedHeader, ChainedHeader latestNewHeader, CheckpointInfo checkpoint, int peerId)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}',{4}.{5}:'{6}')", nameof(chainedHeader), chainedHeader, nameof(latestNewHeader), latestNewHeader, nameof(checkpoint), nameof(checkpoint.Hash), checkpoint.Hash);

            if (checkpoint.Hash != chainedHeader.HashBlock)
            {
                // Make sure that chain with invalid checkpoint in it is removed from the tree.
                // Otherwise a new peer may connect and present headers on top of invalid chain and we wouldn't recognize it.
                this.RemovePeerClaim(peerId, latestNewHeader);

                this.logger.LogDebug("Chained header '{0}' does not match checkpoint '{1}'.", chainedHeader, checkpoint.Hash);
                this.logger.LogTrace("(-)[INVALID_HEADER_NOT_MATCHING_CHECKPOINT]");
                throw new CheckpointMismatchException();
            }

            ChainedHeader subchainTip = chainedHeader;
            if (chainedHeader.Height == this.checkpoints.GetLastCheckpointHeight())
                subchainTip = latestNewHeader;

            ConnectNewHeadersResult connectNewHeadersResult = this.MarkBetterChainAsRequired(subchainTip, latestNewHeader);
            this.MarkTrustedChainAsAssumedValid(chainedHeader);

            this.logger.LogTrace("(-):{0}", connectNewHeadersResult);
            return connectNewHeadersResult;
        }

        /// <summary>
        /// Check whether a header is in one of the following states
        /// <see cref="BlockDataAvailabilityState.BlockAvailable"/>, <see cref="BlockDataAvailabilityState.BlockRequired"/>.
        /// </summary>
        private bool HeaderWasRequested(ChainedHeader chainedHeader)
        {
            return (chainedHeader.BlockDataAvailability == BlockDataAvailabilityState.BlockAvailable)
                  || (chainedHeader.BlockDataAvailability == BlockDataAvailabilityState.BlockRequired);
        }

        /// <summary>
        /// Check whether a header is in one of the following states:
        /// <see cref="ValidationState.AssumedValid"/>, <see cref="ValidationState.PartiallyValidated"/>, <see cref="ValidationState.FullyValidated"/>.
        /// </summary>
        private bool HeaderWasMarkedAsValidated(ChainedHeader chainedHeader)
        {
            return (chainedHeader.BlockValidationState == ValidationState.AssumedValid)
                  || (chainedHeader.BlockValidationState == ValidationState.PartiallyValidated)
                  || (chainedHeader.BlockValidationState == ValidationState.FullyValidated);
        }

        /// <summary>
        /// Remove the branch of the given <see cref="chainedHeader"/> from the tree that is not claimed by any peer .
        /// </summary>
        /// <param name="chainedHeader">The chained header that is the top of the branch.</param>
        private void RemoveUnclaimedBranch(ChainedHeader chainedHeader)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(chainedHeader), chainedHeader);

            ChainedHeader currentHeader = chainedHeader;
            while (currentHeader.Previous != null)
            {
                // If current header is an ancestor of some other tip claimed by a peer, do nothing.
                bool headerHasDecendents = currentHeader.Next.Count > 0;
                if (headerHasDecendents)
                {
                    this.logger.LogTrace("Header '{0}' is part of another branch.", currentHeader);
                    break;
                }

                bool headerIsClaimedByPeer = this.peerIdsByTipHash.ContainsKey(currentHeader.HashBlock);
                if (headerIsClaimedByPeer)
                {
                    this.logger.LogTrace("Header '{0}' is claimed by a peer and won't be removed.", currentHeader);
                    break;
                }

                this.DisconnectChainHeader(currentHeader);

                this.logger.LogTrace("Header '{0}' was removed from the tree.", currentHeader);

                currentHeader = currentHeader.Previous;
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Remove the peer's tip and all the headers claimed by this peer unless they are also claimed by other peers.
        /// </summary>
        /// <param name="networkPeerId">The peer id that is removed.</param>
        /// <param name="chainedHeader">The header where we start walking back the chain from.</param>
        private void RemovePeerClaim(int networkPeerId, ChainedHeader chainedHeader)
        {
            this.logger.LogTrace("({0}:{1},{2}:'{3}')", nameof(networkPeerId), networkPeerId, nameof(chainedHeader), chainedHeader);

            // Collection of peer IDs that claim this chained header as their tip.
            HashSet<int> peerIds;
            if (!this.peerIdsByTipHash.TryGetValue(chainedHeader.HashBlock, out peerIds))
            {
                this.logger.LogTrace("(-)[PEER_TIP_NOT_FOUND]");
                throw new ConsensusException("PEER_TIP_NOT_FOUND");
            }

            this.logger.LogTrace("Tip claim of peer ID {0} removed from chained header '{1}'.", networkPeerId, chainedHeader);
            peerIds.Remove(networkPeerId); // TODO: do we need to throw in this case

            if (peerIds.Count == 0)
            {
                this.logger.LogTrace("Header '{0}' is not the tip of any peer.", chainedHeader);
                this.peerIdsByTipHash.Remove(chainedHeader.HashBlock);
                this.RemoveUnclaimedBranch(chainedHeader);
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Set a new header as a tip for this peer and remove the old tip.
        /// </summary>
        /// <param name="networkPeerId">The peer id that sets a new tip.</param>
        /// <param name="newTip">The new tip to set.</param>
        private void AddOrReplacePeerTip(int networkPeerId, uint256 newTip)
        {
            this.logger.LogTrace("({0}:{1},{2}:'{3}')", nameof(networkPeerId), networkPeerId, nameof(newTip), newTip);

            uint256 oldTipHash = this.peerTipsByPeerId.TryGet(networkPeerId);

            this.ClaimPeerTip(networkPeerId, newTip);

            this.peerTipsByPeerId.AddOrReplace(networkPeerId, newTip);

            if (oldTipHash != null)
            {
                ChainedHeader oldTip = this.chainedHeadersByHash.TryGet(oldTipHash);
                this.RemovePeerClaim(networkPeerId, oldTip); // TODO: do we need to throw if oldTip is null (it is expected to be in the dictionary if its not its a potential bug)
            }

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public ChainedHeader CreateChainedHeaderWithBlock(Block block)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(block), block.GetHash());

            this.CreateNewHeaders(new List<BlockHeader>() {block.Header});

            ChainedHeader chainedHeader = this.FindHeaderAndVerifyBlockIntegrity(block);
            this.BlockDataDownloaded(chainedHeader, block);

            this.logger.LogTrace("(-):'{0}'", chainedHeader);
            return chainedHeader;
        }

        /// <summary>
        /// Find the headers that are not part of the tree and try to connect them to an existing chain
        /// by creating new chained headers and linking them to their previous headers.
        /// </summary>
        /// <remarks>
        /// Header validation is performed on each header.
        /// It will check if the first header violates maximum reorganization rule.
        /// <para>When headers are connected the next pointers of their previous headers are updated.</para>
        /// </remarks>
        /// <param name="headers">The new headers that should be connected to a chain.</param>
        /// <returns>A list of newly created chained headers or <c>null</c> if no new headers were found.</returns>
        /// <exception cref="MaxReorgViolationException">Thrown in case maximum reorganization rule is violated.</exception>
        /// <exception cref="ConnectHeaderException">Thrown if it wasn't possible to connect the first new header.</exception>
        private List<ChainedHeader> CreateNewHeaders(List<BlockHeader> headers)
        {
            this.logger.LogTrace("({0}.{1}:{2})", nameof(headers), nameof(headers.Count), headers.Count);

            if (!this.TryFindNewHeaderIndex(headers, out int newHeaderIndex))
            {
                this.logger.LogTrace("(-)[NO_NEW_HEADERS_FOUND]:null");
                return null;
            }

            ChainedHeader previousChainedHeader;
            if (!this.chainedHeadersByHash.TryGetValue(headers[newHeaderIndex].HashPrevBlock, out previousChainedHeader))
            {
                this.logger.LogTrace("Previous hash '{0}' of block hash '{1}' was not found.", headers[newHeaderIndex].GetHash(), headers[newHeaderIndex].HashPrevBlock);
                this.logger.LogTrace("(-)[PREVIOUS_HEADER_NOT_FOUND]");
                throw new ConnectHeaderException();
            }

            var newChainedHeaders = new List<ChainedHeader>();

            ChainedHeader newChainedHeader = this.CreateAndValidateNewChainedHeader(headers[newHeaderIndex], previousChainedHeader);
            newChainedHeaders.Add(newChainedHeader);
            newHeaderIndex++;

            this.logger.LogTrace("New chained header was added to the tree '{0}'.", newChainedHeader);

            try
            {
                this.CheckMaxReorgRuleViolated(newChainedHeader);

                previousChainedHeader = newChainedHeader;

                for (; newHeaderIndex < headers.Count; newHeaderIndex++)
                {
                    newChainedHeader = this.CreateAndValidateNewChainedHeader(headers[newHeaderIndex], previousChainedHeader);
                    newChainedHeaders.Add(newChainedHeader);
                    this.logger.LogTrace("New chained header was added to the tree '{0}'.", newChainedHeader);

                    previousChainedHeader = newChainedHeader;
                }
            }
            catch
            {
                // Undo changes to the tree. This is necessary because the peer claim wasn't set to the last header yet.
                // So in case of peer disconnection this branch wouldn't be removed.
                // Also not removing this unclaimed branch will allow other peers to present headers on top of invalid chain without us recognizing it.
                this.RemoveUnclaimedBranch(newChainedHeader);

                this.logger.LogTrace("(-)[VALIDATION_FAILED]");
                throw;
            }

            this.logger.LogTrace("(-):*.{0}={1}", nameof(newChainedHeaders.Count), newChainedHeaders.Count);
            return newChainedHeaders;
        }

        private ChainedHeader CreateAndValidateNewChainedHeader(BlockHeader currentBlockHeader, ChainedHeader previousChainedHeader)
        {
            var newChainedHeader = new ChainedHeader(currentBlockHeader, currentBlockHeader.GetHash(), previousChainedHeader);

            this.headerValidator.ValidateHeader(newChainedHeader);
            newChainedHeader.BlockValidationState = ValidationState.HeaderValidated;

            previousChainedHeader.Next.Add(newChainedHeader);
            this.chainedHeadersByHash.Add(newChainedHeader.HashBlock, newChainedHeader);

            return newChainedHeader;
        }

        /// <summary>
        /// Find the first header in the given list of <see cref="headers"/> that does not exist in <see cref="chainedHeadersByHash"/>.
        /// </summary>
        private bool TryFindNewHeaderIndex(List<BlockHeader> headers, out int newHeaderIndex)
        {
            this.logger.LogTrace("({0}.{1}:{2})", nameof(headers), nameof(headers.Count), headers.Count);

            for (newHeaderIndex = 0; newHeaderIndex < headers.Count; newHeaderIndex++)
            {
                uint256 currentBlockHash = headers[newHeaderIndex].GetHash();
                if (!this.chainedHeadersByHash.ContainsKey(currentBlockHash))
                {
                    this.logger.LogTrace("A new header with hash '{0}' was found that is not connected to the tree.", currentBlockHash);
                    this.logger.LogTrace("(-):true,{0}:{1}", nameof(newHeaderIndex), newHeaderIndex);
                    return true;
                }
            }

            this.logger.LogTrace("(-):false");
            return false;
        }

        /// <summary>
        /// Checks if switching to specified <paramref name="chainedHeader"/> would require rewinding consensus behind the finalized block height.
        /// </summary>
        /// <param name="chainedHeader">The header that needs to be checked for reorg.</param>
        /// <exception cref="MaxReorgViolationException">Thrown in case maximum reorganization rule is violated.</exception>
        private void CheckMaxReorgRuleViolated(ChainedHeader chainedHeader)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(chainedHeader), chainedHeader);

            uint maxReorgLength = this.chainState.MaxReorgLength;
            ChainedHeader consensusTip = this.GetConsensusTip();
            if (maxReorgLength != 0)
            {
                ChainedHeader fork = chainedHeader.FindFork(consensusTip);

                if (fork == null)
                {
                    this.logger.LogError("Header '{0}' is from a different network.", chainedHeader);
                    this.logger.LogTrace("(-)[HEADER_IS_INVALID_NETWORK]");
                    throw new InvalidOperationException("Header is from a different network");
                }

                if ((fork != chainedHeader) && (fork != consensusTip))
                {
                    int reorgLength = consensusTip.Height - fork.Height;

                    int finalizedHeight = this.finalizedBlockHeight.GetFinalizedBlockHeight();

                    if (fork.Height < finalizedHeight)
                    {
                        this.logger.LogTrace("Reorganization of length {0} prevented, maximal reorganization length is {1}, consensus tip is '{2}' and the last finalized block height is {3}.", reorgLength, maxReorgLength, consensusTip, finalizedHeight);
                        this.logger.LogTrace("(-)[MAX_REORG_VIOLATION]");
                        throw new MaxReorgViolationException();
                    }

                    this.logger.LogTrace("Reorganization of length {0} accepted, consensus tip is '{1}'.", reorgLength, consensusTip);
                }
            }

            this.logger.LogTrace("(-)");
        }
    }

    /// <summary>
    /// Represents the result of the <see cref="ChainedHeaderTree.ConnectNewHeaders"/> method.
    /// </summary>
    public class ConnectNewHeadersResult
    {
        /// <summary>The earliest header in the chain of the list of headers we are interested in downloading.</summary>
        public ChainedHeader DownloadFrom { get; set; }

        /// <summary>The latest header in the chain of the list of headers we are interested in downloading.</summary>
        public ChainedHeader DownloadTo { get; set; }

        /// <summary>Represents the last processed header from the headers presented by the peer.</summary>
        public ChainedHeader Consumed { get; set; }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{nameof(this.DownloadFrom)}='{this.DownloadFrom}',{nameof(this.DownloadTo)}='{this.DownloadTo}',{nameof(this.Consumed)}='{this.Consumed}'";
        }

        /// <summary>
        /// Convert the <see cref="DownloadFrom" /> and <see cref="DownloadTo" /> to an array
        /// of consecutive headers, both items are included in the array.
        /// </summary>
        /// <returns>Array of consecutive headers.</returns>
        public ChainedHeader[] ToArray()
        {
            return this.DownloadTo.ToChainedHeaderArray(this.DownloadFrom);
        }
    }
}
