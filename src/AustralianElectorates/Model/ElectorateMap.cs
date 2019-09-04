﻿using System.Runtime.Serialization;

namespace AustralianElectorates
{
    [DataContract]
    public class ElectorateMap
    {
        internal ElectorateMap()
        {
        }

        [DataMember]
        public Electorate Electorate { get; internal set; }
        [DataMember]
        public string GeoJson { get; internal set; }
    }
}