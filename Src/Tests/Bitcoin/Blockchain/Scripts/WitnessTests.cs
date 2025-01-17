﻿// Autarkysoft Tests
// Copyright (c) 2020 Autarkysoft
// Distributed under the MIT software license, see the accompanying
// file LICENCE or http://www.opensource.org/licenses/mit-license.php.

using Autarkysoft.Bitcoin;
using Autarkysoft.Bitcoin.Blockchain.Scripts;
using Autarkysoft.Bitcoin.Blockchain.Scripts.Operations;
using System;
using System.Collections.Generic;
using Xunit;

namespace Tests.Bitcoin.Blockchain.Scripts
{
    public class WitnessTests
    {
        [Fact]
        public void Constructor_FromBytesTest()
        {
            byte[][] data = new byte[][] { new byte[] { 1, 2, 3 }, new byte[] { 10, 20 }, new byte[] { 255, 255 } };
            Witness wit = new(data);
            Assert.Equal(data, wit.Items);
        }

        [Fact]
        public void Constructor_NullExceptionTest()
        {
            Assert.Throws<ArgumentNullException>(() => new Witness(null));
        }

        public static IEnumerable<object[]> GetSerCases()
        {
            yield return new object[] { null, new byte[1] };
            yield return new object[] { Array.Empty<PushDataOp>(), new byte[1] };
            yield return new object[]
            {
                new byte[][] { Array.Empty<byte>(), new byte[] { 1, 2, 3 }, new byte[] { 0x53 } },
                new byte[]
                {
                    3, // item count
                    0x00, // OP_0
                    3, 1, 2, 3, // Push 3 bytes
                    1, 0x53 // OP_3
                }
            };
            yield return new object[]
            {
                new byte[][] { Helper.GetBytes(77) }, // 77 is 0x4d as CompactInt and 0x4c4d as StackInt
                Helper.ConcatBytes(79, new byte[]
                {
                    1, // item count
                    0x4d, // Push length as CompactInt
                },
                Helper.GetBytes(77))
            };
            yield return new object[]
            {
                new byte[][] { Helper.GetBytes(255) }, // 255 is 0xfdff00 as CompactInt and 0x4cff as StackInt
                Helper.ConcatBytes(259, new byte[]
                {
                    1, // item count
                    0xfd, // Push length as CompactInt
                    0xff,
                    0x00
                },
                Helper.GetBytes(255))
            };
        }
        [Theory]
        [MemberData(nameof(GetSerCases))]
        public void SerializeTest(byte[][] items, byte[] expected)
        {
            Witness wit = new() { Items = items };
            FastStream stream = new(expected.Length); // Setting length for small optimization
            wit.Serialize(stream);

            Assert.Equal(expected, stream.ToByteArray());
        }

        [Theory]
        [MemberData(nameof(GetSerCases))]
        public void TryDeserializeTest(byte[][] expected, byte[] data)
        {
            Witness wit = new();
            FastStreamReader stream = new(data);
            bool b = wit.TryDeserialize(stream, out string error);

            Assert.True(b, error);
            Assert.Null(error);
            Assert.Equal(expected ?? Array.Empty<byte[]>(), wit.Items);
        }

        public static IEnumerable<object[]> GetSerFailCases()
        {
            yield return new object[] { new byte[] { 253 }, "First byte 253 needs to be followed by at least 2 byte." };
            yield return new object[] { new byte[] { 0xfe, 0x00, 0x00, 0x00, 0x80 }, "Item count is too big." };
            yield return new object[] { new byte[] { 1 }, Err.EndOfStream };
        }
        [Theory]
        [MemberData(nameof(GetSerFailCases))]
        public void TryDeserialize_FailTest(byte[] data, string expErr)
        {
            Witness wit = new();
            FastStreamReader stream = new(data);
            bool b = wit.TryDeserialize(stream, out string error);

            Assert.False(b);
            Assert.Equal(expErr, error);
        }
    }
}
