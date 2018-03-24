﻿using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel;
using Newtonsoft.Json;

namespace CUETools.Codecs.CommandLine
{
    [JsonObject(MemberSerialization.OptIn)]
    public class DecoderSettings : AudioDecoderSettings
    {
        public override string Name => name;

        public override string Extension => extension;

        public DecoderSettings()
            : base()
        {
        }

        public DecoderSettings(
            string _name,
            string _extension,
            string _path,
            string _parameters)
            : base()
        {
            name = _name;
            extension = _extension;
            Path = _path;
            Parameters = _parameters;
        }

        [JsonProperty]
        public string name;

        [JsonProperty]
        public string extension;

        [DefaultValue(null)]
        [JsonProperty]
        public string Path
        {
            get;
            set;
        }

        [DefaultValue(null)]
        [JsonProperty]
        public string Parameters
        {
            get;
            set;
        }
    }
}