using System;
using NUnit.Framework;

namespace LucidLoop.Gyms.Tests
{
    public class RelayAddressPolicyTests
    {
        [TestCase("ws://192.168.1.20:8080/game")]
        [TestCase("ws://10.10.0.4:8080/live")]
        [TestCase("ws://172.16.2.4:8080/game")]
        [TestCase("ws://[fd12::20]:8080/game")]
        [TestCase("ws://relay.local:8080/game")]
        public void EditorAllowsDevelopmentLan(string address)
            => Assert.DoesNotThrow(() => RelayAddressPolicy.Validate(address));

        [TestCase("ws://8.8.8.8:8080/game")]
        [TestCase("ws://172.32.0.1:8080/game")]
        [TestCase("ws://example.com/game")]
        [TestCase("ws://192.168.1.20:8080/game?token=secret")]
        [TestCase("wss://user:password@example.com/game")]
        [TestCase("wss://example.com/game#fragment")]
        public void InvalidOrPublicInsecureAddressesAreRejected(string address)
            => Assert.Throws<ArgumentException>(() => RelayAddressPolicy.Validate(address));

        [TestCase("wss://example.com/game")]
        [TestCase("ws://127.0.0.1:8080/live")]
        public void TlsAndLoopbackRemainSupported(string address)
            => Assert.DoesNotThrow(() => RelayAddressPolicy.Validate(address));
    }
}
