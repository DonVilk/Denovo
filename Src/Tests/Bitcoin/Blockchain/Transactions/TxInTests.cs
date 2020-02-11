﻿// Autarkysoft Tests
// Copyright (c) 2020 Autarkysoft
// Distributed under the MIT software license, see the accompanying
// file LICENCE or http://www.opensource.org/licenses/mit-license.php.

using Autarkysoft.Bitcoin;
using Autarkysoft.Bitcoin.Blockchain.Transactions;
using System;
using System.Collections.Generic;
using Xunit;

namespace Tests.Bitcoin.Blockchain.Transactions
{
    public class TxInTests
    {
        [Fact]
        public void ConstructorTest()
        {
            TxIn tx = new TxIn(new byte[32], 1, null, 0);

            Assert.Equal(new byte[32], tx.TxHash);
            Assert.Equal(1U, tx.Index);
            Assert.Equal(0U, tx.Sequence);
            Assert.NotNull(tx.SigScript);
        }

        [Fact]
        public void Constructor_ExceptionTest()
        {
            Assert.Throws<ArgumentNullException>(() => new TxIn(null, 1, null, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TxIn(new byte[31], 1, null, 0));
        }

        [Fact]
        public void SerializeTest()
        {
            var scr = new MockSerializableSigScript(new byte[1] { 255 });
            TxIn tx = new TxIn(Helper.GetBytes(32), 1, scr, 953132143);
            FastStream stream = new FastStream();
            tx.Serialize(stream);

            byte[] actual = stream.ToByteArray();
            byte[] expected = new byte[32 + 4 + 1 + 4];
            Buffer.BlockCopy(Helper.GetBytes(32), 0, expected, 0, 32);
            expected[32] = 1;
            expected[36] = 255;
            expected[37] = 0x6f;
            expected[38] = 0xa4;
            expected[39] = 0xcf;
            expected[40] = 0x38;

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Serialize_WithScriptTest()
        {
            var scr = new MockSerializableSigScript(new byte[1] { 255 });
            TxIn tx = new TxIn(Helper.GetBytes(32), 1, null, 953132143);
            FastStream stream = new FastStream();
            tx.Serialize(stream, scr, false);

            byte[] actual = stream.ToByteArray();
            byte[] expected = new byte[32 + 4 + 1 + 4];
            Buffer.BlockCopy(Helper.GetBytes(32), 0, expected, 0, 32);
            expected[32] = 1;
            expected[36] = 255;
            expected[37] = 0x6f;
            expected[38] = 0xa4;
            expected[39] = 0xcf;
            expected[40] = 0x38;

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Serialize_WithScript_SeqChangeTest()
        {
            var scr = new MockSerializableSigScript(new byte[1] { 255 });
            TxIn tx = new TxIn(Helper.GetBytes(32), 2, null, 953132143);
            FastStream stream = new FastStream();
            tx.Serialize(stream, scr, true);

            byte[] actual = stream.ToByteArray();
            byte[] expected = new byte[32 + 4 + 1 + 4];
            Buffer.BlockCopy(Helper.GetBytes(32), 0, expected, 0, 32);
            expected[32] = 2;
            expected[36] = 255;

            Assert.Equal(expected, actual);
        }


        public static IEnumerable<object[]> GetDeserCases()
        {
            yield return new object[] { new byte[41], new MockDeserializableSigScript(36, 1), new byte[32], 0, 0 };
            yield return new object[] 
            {
                Helper.HexToBytes("a5c63f45d7f03633aec127b2821c181ea326044e9ab20d2abaf20bafffe79c4e"+"7b000000"+"ff"+"e73403b3"),
                new MockDeserializableSigScript(36, 1),
                Helper.HexToBytes("a5c63f45d7f03633aec127b2821c181ea326044e9ab20d2abaf20bafffe79c4e"),
                123,
                3003331815
            };
        }
        [Theory]
        [MemberData(nameof(GetDeserCases))]
        public void TryDeserializeTest(byte[] data, MockDeserializableSigScript scr, byte[] expHash, uint expIndex, uint expSeq)
        {
            TxIn tx = new TxIn()
            {
                SigScript = scr
            };
            FastStreamReader stream = new FastStreamReader(data);
            bool b = tx.TryDeserialize(stream, out string error);

            Assert.True(b, error);
            Assert.Null(error);
            Assert.Equal(expHash, tx.TxHash);
            Assert.Equal(expIndex, tx.Index);
            Assert.Equal(expSeq, tx.Sequence);
            // Mock script has its own tests.
        }

        public static IEnumerable<object[]> GetDeserFailCases()
        {
            yield return new object[] { null, null, "Stream can not be null." };
            yield return new object[] { new FastStreamReader(new byte[31]), null, Err.EndOfStream };
            yield return new object[] { new FastStreamReader(new byte[35]), null, Err.EndOfStream };
            yield return new object[]
            {
                new FastStreamReader(new byte[37]),
                new MockDeserializableSigScript(36, 0, "Foo"),
                "Foo"
            };
            yield return new object[]
            {
                new FastStreamReader(new byte[40]),
                new MockDeserializableSigScript(36, 1),
                Err.EndOfStream
            };
        }
        [Theory]
        [MemberData(nameof(GetDeserFailCases))]
        public void TryDeserialize_FailTest(FastStreamReader stream, MockDeserializableSigScript scr, string expErr)
        {
            TxIn tx = new TxIn()
            {
                SigScript = scr
            };
            bool b = tx.TryDeserialize(stream, out string error);

            Assert.False(b);
            Assert.Equal(expErr, error);
        }
    }
}