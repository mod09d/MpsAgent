﻿using System;
using System.Collections.Generic;
using System.Text;

namespace VmAgent.Core.UnitTests
{
    using System.Globalization;
    using FluentAssertions;
    using Microsoft.Azure.Gaming.AgentInterfaces;
    using Microsoft.Azure.Gaming.VmAgent.Core;
    using Microsoft.Azure.Gaming.VmAgent.Extensions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class VmConfigurationTests
    {
        private static readonly string PlayFabTitleId = "ABC123";

        private static readonly Guid DeploymentId = Guid.NewGuid();

        private static readonly string Region = "SouthCentralUs";

        private static readonly VmConfiguration VmConfiguration = new VmConfiguration(56001, Guid.NewGuid().ToString(), new VmDirectories("C:\\windows\\temp"));

        [TestMethod]
        [TestCategory("BVT")]
        public void EnvVariablesWithBuildMetadata()
        {
            var metadata = new Dictionary<string, string>() { { "key1", "value1" }, { "key2", "value2" } };
            SessionHostsStartInfo sessionHostsStartInfo = CreateSessionHostStartInfo(metadata);
            IDictionary<string, string> envVariables = VmConfiguration.GetCommonEnvironmentVariables(sessionHostsStartInfo, VmConfiguration);
            ValidateCommonEnvironmentVariables(envVariables, sessionHostsStartInfo);
        }

        [TestMethod]
        [TestCategory("BVT")]
        public void EnvVariablesWithoutBuildMetadata()
        {
            SessionHostsStartInfo sessionHostsStartInfo = CreateSessionHostStartInfo();
            IDictionary<string, string> envVariables = VmConfiguration.GetCommonEnvironmentVariables(sessionHostsStartInfo, VmConfiguration);
            ValidateCommonEnvironmentVariables(envVariables, sessionHostsStartInfo);
        }

        [TestMethod]
        [TestCategory("BVT")]
        public void GameSharedContentFolderWithProcess()
        {
            SessionHostsStartInfo sessionHostsStartInfo = CreateSessionHostStartInfo(sessionHostType: SessionHostType.Process);
            IDictionary<string, string> envVariables = VmConfiguration.GetCommonEnvironmentVariables(sessionHostsStartInfo, VmConfiguration);
            ValidateCommonEnvironmentVariables(envVariables, sessionHostsStartInfo);

            Assert.AreEqual(VmConfiguration.VmDirectories.GameSharedContentFolderVm, envVariables[VmConfiguration.SharedContentFolderEnvVariable]);
        }

        [TestMethod]
        [TestCategory("BVT")]
        public void GameSharedContentFolderWithContainer()
        {
            SessionHostsStartInfo sessionHostsStartInfo = CreateSessionHostStartInfo(sessionHostType: SessionHostType.Container);
            IDictionary<string, string> envVariables = VmConfiguration.GetCommonEnvironmentVariables(sessionHostsStartInfo, VmConfiguration);
            ValidateCommonEnvironmentVariables(envVariables, sessionHostsStartInfo);

            Assert.AreEqual(VmConfiguration.VmDirectories.GameSharedContentFolderContainer, envVariables[VmConfiguration.SharedContentFolderEnvVariable]);
        }

        [TestMethod]
        [TestCategory("BVT")]
        public void VmStartupScriptEnvVariablesWithBuildMetadata()
        {
            var metadata = new Dictionary<string, string>() { { "key1", "value1" }, { "key2", "value2" } };
            SessionHostsStartInfo sessionHostsStartInfo = CreateSessionHostStartInfo(metadata);
            IDictionary<string, string> envVariables = VmConfiguration.GetEnvironmentVariablesForVmStartupScripts(sessionHostsStartInfo, VmConfiguration);
            ValidateVmScriptEnvironmentVariables(envVariables, sessionHostsStartInfo);
        }

        [TestMethod]
        [TestCategory("BVT")]
        public void VmStartupScriptEnvVariablesWithPorts()
        {
            SessionHostsStartInfo sessionHostsStartInfo = CreateSessionHostStartInfo(new Dictionary<string, string>());
            sessionHostsStartInfo.VmStartupScriptConfiguration = new VmStartupScriptConfiguration()
            {
                Ports = new VmStartupScriptPort[]
                {
                    new VmStartupScriptPort() { Name="port0", PublicPort = 20010, NodePort=20000, Protocol = "TCP" },
                    new VmStartupScriptPort() { Name="port1", PublicPort = 20011, NodePort=20001, Protocol = "UDP" },
                }
            };
            IDictionary<string, string> envVariables = VmConfiguration.GetEnvironmentVariablesForVmStartupScripts(sessionHostsStartInfo, VmConfiguration);
            ValidateVmScriptEnvironmentVariables(envVariables, sessionHostsStartInfo);
            envVariables.Should().Contain("PF_STARTUP_SCRIPT_PORT_COUNT", "2");
            for (int i = 0; i < 2; i++)
            {
                envVariables.Should().Contain($"PF_STARTUP_SCRIPT_PORT_NAME_{i}", $"port{i}");
                envVariables.Should().Contain($"PF_STARTUP_SCRIPT_PORT_EXTERNAL_{i}", (20010+i).ToString());
                envVariables.Should().Contain($"PF_STARTUP_SCRIPT_PORT_PROTOCOL_{i}", i == 0 ? "TCP" : "UDP");
                envVariables.Should().Contain($"PF_STARTUP_SCRIPT_PORT_INTERNAL_{i}", (20000+i).ToString());
            }
        }

        [TestMethod]
        [TestCategory("BVT")]
        public void EnvironmentVariablesWithMultipleIpAddresses()
        {
            SessionHostsStartInfo sessionHostsStartInfo = CreateSessionHostStartInfo(new Dictionary<string, string>());
            sessionHostsStartInfo.PublicIpAddresses = new List<PublicIpAddress>()
            {
                new PublicIpAddress()
                {
                    IpAddress = "127.0.0.1",
                    FQDN = "myFQDN",
                    RoutingType = RoutingType.Internet
                },
                new PublicIpAddress()
                {
                    IpAddress = "127.0.0.2",
                    FQDN = "myFQDN2",
                    RoutingType = RoutingType.Microsoft
                },
            };

            IDictionary<string, string> envVariables = VmConfiguration.GetCommonEnvironmentVariables(sessionHostsStartInfo, VmConfiguration);
            envVariables.Should().Contain("PF_PUBLIC_IP_ADDRESSES_COUNT", "2");
            envVariables.Should().Contain("PF_PUBLIC_IP_ADDRESSES_IP_ADDRESS_0", "127.0.0.1");
            envVariables.Should().Contain("PF_PUBLIC_IP_ADDRESSES_FQDN_0", "myFQDN");
            envVariables.Should().Contain("PF_PUBLIC_IP_ADDRESSES_ROUTING_TYPE_0", "Internet");
            envVariables.Should().Contain("PF_PUBLIC_IP_ADDRESSES_IP_ADDRESS_1", "127.0.0.2");
            envVariables.Should().Contain("PF_PUBLIC_IP_ADDRESSES_FQDN_1", "myFQDN2");
            envVariables.Should().Contain("PF_PUBLIC_IP_ADDRESSES_ROUTING_TYPE_1", "Microsoft");
        }

        [TestMethod]
        [TestCategory("BVT")]
        public void EnvironmentVariablesWithMpsSecrets()
        {
            SessionHostsStartInfo sessionHostsStartInfo = CreateSessionHostStartInfo(new Dictionary<string, string>());
            sessionHostsStartInfo.GameSecrets = new SecretDetail[]
            {
                new SecretDetail()
                {
                    Name = "secret1",
                    Value = "value1"
                },
                new SecretDetail()
                {
                    Name = "secret2",
                    Value = "value2"
                }
            };

            IDictionary<string, string> envVariables = VmConfiguration.GetCommonEnvironmentVariables(sessionHostsStartInfo, VmConfiguration);
            envVariables.Should().Contain("PF_MPS_SECRET_secret1", "value1");
            envVariables.Should().Contain("PF_MPS_SECRET_secret2", "value2");
        }


        private SessionHostsStartInfo CreateSessionHostStartInfo(IDictionary<string, string> buildMetadata = null, SessionHostType sessionHostType = SessionHostType.Process)
        {
            return new SessionHostsStartInfo
            {
                AssignmentId = CreateAssignmentId(),
                DeploymentMetadata = buildMetadata,
                PublicIpV4Address = "42.42.42.42",
                SessionHostType = sessionHostType
            };
        }

        private void ValidateCommonEnvironmentVariables(IDictionary<string, string> envVariables, SessionHostsStartInfo sessionHostsStartInfo, bool containsMultipleIps = false)
        {
            envVariables.Should().Contain(VmConfiguration.PublicIPv4AddressEnvVariable, sessionHostsStartInfo.PublicIpV4Address);
            envVariables.Should().Contain(VmConfiguration.PublicIPv4AddressEnvVariableV2, sessionHostsStartInfo.PublicIpV4Address);
            envVariables.Should().Contain(VmConfiguration.FqdnEnvVariable, sessionHostsStartInfo.FQDN);
            envVariables.Should().Contain(VmConfiguration.TitleIdEnvVariable, PlayFabTitleId);
            envVariables.Should().Contain(VmConfiguration.BuildIdEnvVariable, DeploymentId.ToString());
            envVariables.Should().Contain(VmConfiguration.RegionEnvVariable, Region);
            sessionHostsStartInfo.DeploymentMetadata?.ForEach(x => envVariables.Should().Contain(x.Key, x.Value));

            if (!containsMultipleIps)
            {
                envVariables.Should().Contain("PF_PUBLIC_IP_ADDRESSES_COUNT", "1");
                envVariables.Should().Contain("PF_PUBLIC_IP_ADDRESSES_IP_ADDRESS_0", sessionHostsStartInfo.PublicIpV4Address);
                envVariables.Should().Contain("PF_PUBLIC_IP_ADDRESSES_FQDN_0", sessionHostsStartInfo.FQDN);
                envVariables.Should().Contain("PF_PUBLIC_IP_ADDRESSES_ROUTING_TYPE_0", "Microsoft");
            }
        }

        private void ValidateVmScriptEnvironmentVariables(IDictionary<string, string> envVariables, SessionHostsStartInfo sessionHostsStartInfo)
        {
            ValidateCommonEnvironmentVariables(envVariables, sessionHostsStartInfo);
            envVariables.Should().Contain(VmConfiguration.SharedContentFolderVmVariable, VmConfiguration.VmDirectories.GameSharedContentFolderVm);
        }

        private string CreateAssignmentId()
        {
            var titleIdGuid = VmConfiguration.GetGuidFromTitleId(ulong.Parse(PlayFabTitleId, NumberStyles.HexNumber));
            return $"{titleIdGuid}{VmConfiguration.AssignmentIdSeparator}{DeploymentId}{VmConfiguration.AssignmentIdSeparator}{Region}";
        }
    }
}
