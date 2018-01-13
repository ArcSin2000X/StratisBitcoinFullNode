﻿using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using Stratis.SmartContracts.State;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class SmartContractRepositoryTests
    {
        private static readonly byte[] empty = new byte[0];
        private static readonly byte[] dog = Encoding.UTF8.GetBytes("dog");
        private static readonly byte[] dodecahedron = Encoding.UTF8.GetBytes("dodecahedron");
        private static readonly byte[] cat = Encoding.UTF8.GetBytes("cat");
        private static readonly byte[] fish = Encoding.UTF8.GetBytes("fish");
        private static readonly byte[] bird = Encoding.UTF8.GetBytes("bird");

        [Fact]
        public void IntegrationRepositoryTest()
        {
            uint160 testAddress = 111111;

            SmartContractStateRepository repo = new SmartContractStateRepository();
            repo.CreateAccount(testAddress);
            repo.SetObject(testAddress, dog, cat);
            Assert.Equal(cat, repo.GetObject<byte[]>(testAddress, dog));

        }
    }
}