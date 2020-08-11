﻿// Autarkysoft Tests
// Copyright (c) 2020 Autarkysoft
// Distributed under the MIT software license, see the accompanying
// file LICENCE or http://www.opensource.org/licenses/mit-license.php.

using Autarkysoft.Bitcoin;
using Autarkysoft.Bitcoin.Blockchain;
using Autarkysoft.Bitcoin.P2PNetwork;
using Autarkysoft.Bitcoin.P2PNetwork.Messages;
using Autarkysoft.Bitcoin.P2PNetwork.Messages.MessagePayloads;
using System;
using System.Collections.Generic;
using System.Net;
using Tests.Bitcoin.Blockchain;
using Xunit;

namespace Tests.Bitcoin.P2PNetwork
{
    public class ReplyManagerTests
    {
        [Fact]
        public void GetVersionMsgTest()
        {
            var ns = new MockNodeStatus();
            var cs = new MockClientSettings()
            {
                _protoVer = 123,
                _services = NodeServiceFlags.All,
                _time = 456,
                _port = 789,
                _ua = "foo",
                _relay = true,
                _netType = NetworkType.TestNet
            };
            var bc = new MockBlockchain()
            {
                _height = 12345
            };

            var rep = new ReplyManager(ns, bc, cs)
            {
                rng = new MockNonceRng(0x0158a8e8ba5f3ed3)
            };


            Message msg = rep.GetVersionMsg();
            FastStream actual = new FastStream();
            msg.Serialize(actual);
            byte[] expected = Helper.HexToBytes("0b11090776657273696f6e0000000000590000000ba371327b0000001f04000000000000c8010000000000001f0400000000000000000000000000000000ffff7f00000103151f0400000000000000000000000000000000ffff7f0000010315d33e5fbae8a8580103666f6f3930000001");

            Assert.Equal(expected, actual.ToByteArray());
        }

        public static IEnumerable<object[]> GetReplyCases()
        {
            var cs = new MockClientSettings() { _netType = NetworkType.MainNet };
            var bc = new MockBlockchain();

            yield return new object[]
            {
                new MockNodeStatus() { _handShakeToReturn = HandShakeState.Finished, updateTime = true },
                cs, bc,
                new Message(new PingPayload(98765), NetworkType.MainNet),
                new Message[1] { new Message(new PongPayload(98765), NetworkType.MainNet) }
            };
            yield return new object[]
            {
                new MockNodeStatus() { _handShakeToReturn = HandShakeState.Finished, updateTime = true },
                cs, bc,
                new Message(new PongPayload(98765), NetworkType.MainNet),
                null
            };
        }
        [Theory]
        [MemberData(nameof(GetReplyCases))]
        public void GetReplyTest(MockNodeStatus ns, IClientSettings cs, IBlockchain bc, Message msg, Message[] expected)
        {
            var rep = new ReplyManager(ns, bc, cs);

            Message[] actual = rep.GetReply(msg);

            if (expected is null)
            {
                Assert.Null(actual);
            }
            else
            {
                Assert.NotNull(actual);
                Assert.Equal(expected.Length, actual.Length);
                for (int i = 0; i < expected.Length; i++)
                {
                    var actualStream = new FastStream(Constants.MessageHeaderSize + actual[i].PayloadData.Length);
                    var expectedStream = new FastStream(Constants.MessageHeaderSize + expected[i].PayloadData.Length);
                    actual[i].Serialize(actualStream);
                    expected[i].Serialize(expectedStream);

                    Assert.Equal(expectedStream.ToByteArray(), actualStream.ToByteArray());
                }
            }

            // Mock will change the following bool to false if it were called.
            Assert.False(ns.updateTime, "UpdateTime() was never called");
        }

        public static IEnumerable<object[]> GetDeserFailCases()
        {
            foreach (PayloadType item in Enum.GetValues(typeof(PayloadType)))
            {
                if (item != PayloadType.Alert && item != PayloadType.Reject && // Ignored messages
                    item != PayloadType.Verack && item != PayloadType.Version && // have separate tests
                    item != PayloadType.FilterClear && item != PayloadType.GetAddr &&
                    item != PayloadType.MemPool && item != PayloadType.SendHeaders) // Empty payload
                {
                    yield return new object[] { item };
                }
            }
        }
        [Theory]
        [MemberData(nameof(GetDeserFailCases))]
        public void GetReply_FailToDeserializeTest(PayloadType plt)
        {
            var msg = new Message(new MockSerializableMessagePayload(plt, new byte[] { 255, 255, 255 }), NetworkType.MainNet);
            var ns = new MockNodeStatus()
            {
                _handShakeToReturn = HandShakeState.Finished,
                smallViolation = true,
                updateTime = true
            };
            var rep = new ReplyManager(ns, new MockBlockchain(), new ClientSettings());

            Message[] actual = rep.GetReply(msg);
            Assert.Null(actual);

            Assert.False(ns.updateTime);
            Assert.False(ns.smallViolation);
        }

        [Fact]
        public void GetReply_UndefinedPayloadTest()
        {
            var pl = new MockSerializableMessagePayload((PayloadType)10000, new byte[] { 1, 2, 3 });
            var msg = new Message(pl, NetworkType.MainNet);
            var ns = new MockNodeStatus()
            {
                _handShakeToReturn = HandShakeState.Finished,
                smallViolation = true,
                updateTime = true
            };
            var rep = new ReplyManager(ns, new MockBlockchain(), new MockClientSettings());

            var actual = rep.GetReply(msg);

            Assert.Null(actual);
            Assert.False(ns.updateTime);
        }

        [Fact]
        public void GetReply_NoHandShakeTest()
        {
            var msg = new Message(new GetAddrPayload(), NetworkType.MainNet);
            var ns = new MockNodeStatus()
            {
                _handShakeToReturn = HandShakeState.None,
                mediumViolation = true,
                updateTime = true
            };
            var rep = new ReplyManager(ns, new MockBlockchain(), new MockClientSettings());

            var actual = rep.GetReply(msg);

            Assert.Null(actual);
            Assert.False(ns.updateTime);
        }

        public static IEnumerable<object[]> GetIgnoredCases()
        {
            yield return new object[]
            {
                new Message(new MockSerializableMessagePayload(PayloadType.Alert, new byte[1]), NetworkType.MainNet)
            };
            yield return new object[]
            {
                new Message(new MockSerializableMessagePayload(PayloadType.Reject, new byte[1]), NetworkType.MainNet)
            };
        }
        [Theory]
        [MemberData(nameof(GetIgnoredCases))]
        public void GetReply_IgnoredMessagesTest(Message msg)
        {
            var ns = new MockNodeStatus()
            {
                _handShakeToReturn = HandShakeState.Finished,
                updateTime = true
            };
            var rep = new ReplyManager(ns, new MockBlockchain(), new MockClientSettings());

            var actual = rep.GetReply(msg);

            Assert.Null(actual);
            Assert.False(ns.updateTime);
        }

        public static IEnumerable<object[]> GetVerackCases()
        {
            yield return new object[]
            {
                new MockNodeStatus() { _handShakeToReturn = HandShakeState.None, mediumViolation = true, updateTime = true },
            };
            yield return new object[]
            {
                new MockNodeStatus()
                {
                    _handShakeToReturn = HandShakeState.ReceivedAndReplied,
                    _handShakeToSet = HandShakeState.Finished,
                    updateTime = true
                },
            };
            yield return new object[]
            {
                new MockNodeStatus()
                {
                    _handShakeToReturn = HandShakeState.Sent,
                    _handShakeToSet = HandShakeState.SentAndConfirmed,
                    updateTime = true
                },
            };
            yield return new object[]
            {
                new MockNodeStatus()
                {
                    _handShakeToReturn = HandShakeState.SentAndConfirmed, mediumViolation = true, updateTime = true
                },
            };
            yield return new object[]
            {
                new MockNodeStatus()
                {
                    _handShakeToReturn = HandShakeState.SentAndReceived,
                    _handShakeToSet = HandShakeState.Finished,
                    updateTime = true
                },
            };
            yield return new object[]
            {
                new MockNodeStatus()
                {
                    _handShakeToReturn = HandShakeState.Finished, mediumViolation = true, updateTime = true
                },
            };
        }
        [Theory]
        [MemberData(nameof(GetVerackCases))]
        public void CheckVerackTest(MockNodeStatus ns)
        {
            var rep = new ReplyManager(ns, new MockBlockchain(), new MockClientSettings());
            var msg = new Message(new VerackPayload(), NetworkType.MainNet);

            Message[] actual = rep.GetReply(msg);
            Assert.Null(actual);

            // Mock will change the following bool to false if it were called.
            Assert.False(ns.updateTime, "UpdateTime() was never called");

            // Mock either doesn't have any h.s. to set or if it did set h.s. it was checked and then turned to null
            Assert.Null(ns._handShakeToSet);
        }

        public static IEnumerable<object[]> GetVersionCases()
        {
            var cs = new MockClientSettings()
            {
                _protoVer = 123,
                _services = NodeServiceFlags.All,
                _time = 456,
                _port = 789,
                _ua = "foo",
                _relay = true,
                _netType = NetworkType.MainNet
            };
            var bc = new MockBlockchain()
            {
                _height = 12345
            };
            var verPl = new VersionPayload();
            Assert.True(verPl.TryDeserialize(new FastStreamReader(Helper.HexToBytes("721101000100000000000000bc8f5e5400000000010000000000000000000000000000000000ffffc61b6409208d010000000000000000000000000000000000ffffcb0071c0208d128035cbc97953f80f2f5361746f7368693a302e392e332fcf05050001")), out string error), error);
            var msg = new Message(verPl, NetworkType.MainNet);
            var rcv = new NetworkAddress(NodeServiceFlags.NodeNetwork, IPAddress.Parse("203.0.113.192"), 8333);
            var trs = new NetworkAddress(NodeServiceFlags.All, IPAddress.Loopback, 789);
            var verak = new Message(new VerackPayload(), NetworkType.MainNet);
            var ver = new Message(new VersionPayload(123, 456, rcv, trs, 0x0158a8e8ba5f3ed3, "foo", 12345, true), NetworkType.MainNet);

            yield return new object[]
            {
                new MockNodeStatus()
                {
                    _handShakeToReturn = HandShakeState.None,
                    _handShakeToSet = HandShakeState.ReceivedAndReplied,
                    updateTime = true
                },
                cs, bc, msg, new Message[] { verak, ver }
            };
            yield return new object[]
            {
                new MockNodeStatus()
                {
                    _handShakeToReturn = HandShakeState.ReceivedAndReplied,
                    mediumViolation = true,
                    updateTime = true
                },
                cs, bc, msg, null
            };
            yield return new object[]
            {
                new MockNodeStatus()
                {
                    _handShakeToReturn = HandShakeState.Sent,
                    _handShakeToSet = HandShakeState.SentAndReceived,
                    updateTime = true
                },
                cs, bc, msg, new Message[] { verak }
            };
            yield return new object[]
            {
                new MockNodeStatus()
                {
                    _handShakeToReturn = HandShakeState.SentAndConfirmed,
                    _handShakeToSet = HandShakeState.Finished,
                    updateTime = true
                },
                cs, bc, msg, new Message[] { verak }
            };
            yield return new object[]
            {
                new MockNodeStatus()
                {
                    _handShakeToReturn = HandShakeState.SentAndReceived,
                    mediumViolation = true,
                    updateTime = true
                },
                cs, bc, msg, null
            };
            yield return new object[]
            {
                new MockNodeStatus()
                {
                    _handShakeToReturn = HandShakeState.Finished,
                    mediumViolation = true,
                    updateTime = true
                },
                cs, bc, msg, null
            };
        }
        [Theory]
        [MemberData(nameof(GetVersionCases))]
        public void CheckVersionTest(MockNodeStatus ns, IClientSettings cs, IBlockchain bc, Message msg, Message[] expected)
        {
            var rep = new ReplyManager(ns, bc, cs)
            {
                rng = new MockNonceRng(0x0158a8e8ba5f3ed3)
            };

            Message[] actual = rep.GetReply(msg);

            if (expected is null)
            {
                Assert.Null(actual);
            }
            else
            {
                Assert.NotNull(actual);
                Assert.Equal(expected.Length, actual.Length);
                for (int i = 0; i < expected.Length; i++)
                {
                    var actualStream = new FastStream(Constants.MessageHeaderSize + actual[i].PayloadData.Length);
                    var expectedStream = new FastStream(Constants.MessageHeaderSize + expected[i].PayloadData.Length);
                    actual[i].Serialize(actualStream);
                    expected[i].Serialize(expectedStream);

                    Assert.Equal(expectedStream.ToByteArray(), actualStream.ToByteArray());
                }
            }

            // Mock will change the following bool to false if it were called.
            Assert.False(ns.updateTime, "UpdateTime() was never called");

            // Mock either doesn't have any h.s. to set or if it did set h.s. it was checked and then turned to null
            Assert.Null(ns._handShakeToSet);
        }
    }
}