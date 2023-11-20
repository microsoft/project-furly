// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure
{
    using AutoFixture;
    using System;
    using Xunit;
    using Xunit.Categories;

    [UnitTest]
    public class HubResourceTests
    {
        [Fact]
        public void TestFormatParse1()
        {
            var fix = new Fixture();
            var hub = fix.Create<string>();
            var device = fix.Create<string>();
            var module = fix.Create<string>();

            var target = HubResource.Format(hub, device, module);
            var success = HubResource.Parse(target, out var h, out var d, out var m, out var e);

            Assert.True(success);
            Assert.Null(e);
            Assert.Equal(hub, h);
            Assert.Equal(device, d);
            Assert.Equal(module, m);
        }

        [Fact]
        public void TestFormatParse2a()
        {
            var fix = new Fixture();
            var hub = fix.Create<string>();
            var device = fix.Create<string>();

            var target = HubResource.Format(hub, device, null);
            var success = HubResource.Parse(target, out var h, out var d, out var m, out var e);

            Assert.True(success);
            Assert.Null(e);

            Assert.Equal(hub, h);
            Assert.Equal(device, d);
            Assert.Null(m);
        }

        [Fact]
        public void TestFormatParse2b()
        {
            var fix = new Fixture();
            var hub = fix.Create<string>();
            var device = fix.Create<string>();

            var target = HubResource.Format(hub, device, "");
            var success = HubResource.Parse(target, out var h, out var d, out var m, out var e);

            Assert.True(success);
            Assert.Null(e);

            Assert.Equal(hub, h);
            Assert.Equal(device, d);
            Assert.Null(m);
        }

        [Fact]
        public void TestFormatParse2c()
        {
            var fix = new Fixture();
            var hub = "_" + fix.Create<string>() + "_device_";
            var device = fix.Create<string>() + "_module_publisher";

            var target = HubResource.Format(hub, device, "");
            var success = HubResource.Parse(target, out var h, out var d, out var m, out var e);

            Assert.True(success);
            Assert.Null(e);

            Assert.Equal(hub, h);
            Assert.Equal(device, d);
            Assert.Null(m);
        }

        [Fact]
        public void TestFormatParse3()
        {
            var fix = new Fixture();
            var device = fix.Create<string>();
            var module = fix.Create<string>();

            var target = HubResource.Format(null, device, module);
            var success = HubResource.Parse(target, out var h, out var d, out var m, out var e);

            Assert.True(success);
            Assert.Null(e);

            Assert.Null(h);
            Assert.Equal(device, d);
            Assert.Equal(module, m);
        }

        [Fact]
        public void TestFormatParse3a()
        {
            var fix = new Fixture();
            var device = fix.Create<string>() + "_386334";
            var module = fix.Create<string>();

            var target = HubResource.Format(null, device, module);
            var success = HubResource.Parse(target, out var h, out var d, out var m, out var e);

            Assert.True(success);
            Assert.Null(e);

            Assert.Null(h);
            Assert.Equal(device, d);
            Assert.Equal(module, m);
        }

        [Fact]
        public void TestFormatParse3b()
        {
            var fix = new Fixture();
            var device = fix.Create<string>() + "_module";
            var module = fix.Create<string>();

            var target = HubResource.Format(null, device, module);
            var success = HubResource.Parse(target, out var h, out var d, out var m, out var e);

            Assert.True(success);
            Assert.Null(e);

            Assert.Null(h);
            Assert.Equal(device, d);
            Assert.Equal(module, m);
        }

        [Fact]
        public void TestFormatParse3c()
        {
            var fix = new Fixture();
            var device = "_" + fix.Create<string>() + "_386334";
            var module = "_module_" + fix.Create<string>() + "_/+_333666";

            var target = HubResource.Format(null, device, module);
            var success = HubResource.Parse(target, out var h, out var d, out var m, out var e);

            Assert.True(success);
            Assert.Null(e);

            Assert.Null(h);
            Assert.Equal(device, d);
            Assert.Equal(module, m);
        }

        [Fact]
        public void TestFormatParse4a()
        {
            var fix = new Fixture();
            var device = fix.Create<string>();

            var target = HubResource.Format(null, device, null);
            var success = HubResource.Parse(target, out var h, out var d, out var m, out var e);

            Assert.True(success);
            Assert.Null(e);

            Assert.Null(h);
            Assert.Equal(device, d);
            Assert.Null(m);
        }

        [Fact]
        public void TestFormatParse4b()
        {
            var fix = new Fixture();
            var device = fix.Create<string>();

            var target = HubResource.Format("", device, null);
            var success = HubResource.Parse(target, out var h, out var d, out var m, out var e);

            Assert.True(success);
            Assert.Null(e);

            Assert.Null(h);
            Assert.Equal(device, d);
            Assert.Null(m);
        }

        [Fact]
        public void TestFormatParse5()
        {
            var fix = new Fixture();
            const string hub = "a.b.com";
            var device = fix.Create<string>();
            var module = fix.Create<string>();

            var target = HubResource.Format(hub, device, module);
            var success = HubResource.Parse(target, out var h, out var d, out var m, out var e);

            Assert.True(success);
            Assert.Null(e);

            Assert.Equal("a", h);
            Assert.Equal(device, d);
            Assert.Equal(module, m);
        }

        [Fact]
        public void TestParseErrors1()
        {
            Assert.False(HubResource.Parse("hub/x", out var h, out var d, out var m, out var e));
            Assert.Null(h);
            Assert.Null(d);
            Assert.Null(m);
            Assert.NotNull(e);
        }

        [Fact]
        public void TestParseErrors2()
        {
            Assert.False(HubResource.Parse("hub/devices", out var h, out var d, out var m, out var e));
            Assert.Null(h);
            Assert.Null(d);
            Assert.Null(m);
            Assert.NotNull(e);
        }

        [Fact]
        public void TestParseErrors3()
        {
            Assert.False(HubResource.Parse("hub/devices/did/fid", out var h, out var d, out var m, out var e));
            Assert.Null(h);
            Assert.Null(d);
            Assert.Null(m);
            Assert.NotNull(e);
        }

        [Fact]
        public void TestParseErrors4()
        {
            Assert.False(HubResource.Parse("hub/devices/did/modules/mid/bif", out var h, out var d, out var m, out var e));
            Assert.Null(h);
            Assert.Null(d);
            Assert.Null(m);
            Assert.NotNull(e);
        }

        [Fact]
        public void TestParseErrors5()
        {
            Assert.False(HubResource.Parse("hub_x", out var h, out var d, out var m, out var e));
            Assert.Null(h);
            Assert.Null(d);
            Assert.Null(m);
            Assert.NotNull(e);
        }

        [Fact]
        public void TestParseErrors6()
        {
            Assert.False(HubResource.Parse("hub_device", out var h, out var d, out var m, out var e));
            Assert.Null(h);
            Assert.Null(d);
            Assert.Null(m);
            Assert.NotNull(e);
        }

        [Fact]
        public void TestParseErrors7()
        {
            Assert.False(HubResource.Parse("hub_device_did_fid", out var h, out var d, out var m, out var e));
            Assert.Null(h);
            Assert.Null(d);
            Assert.Null(m);
            Assert.NotNull(e);
        }

        [Fact]
        public void TestParseErrors8()
        {
            Assert.False(HubResource.Parse("hub_device_did_module_mid_bif", out var h, out var d, out var m, out var e));
            Assert.Null(h);
            Assert.Null(d);
            Assert.Null(m);
            Assert.NotNull(e);
        }

        [Fact]
        public void TestArguments()
        {
            Assert.Throws<ArgumentException>(() => HubResource.Parse("hub_device_did_module_mid_bif",
                out var h, out var d, out var m, out var e, '+'));
            Assert.Throws<ArgumentException>(() => HubResource.Parse("hub_device_did_module_mid_bif",
                out var h, out var d, out var m, out var e, ':'));
            Assert.Throws<ArgumentException>(() => HubResource.Format("hub", "device", "module", '+'));
            Assert.Throws<ArgumentException>(() => HubResource.Format("hub", "device", "module", 'x'));
        }
    }
}
