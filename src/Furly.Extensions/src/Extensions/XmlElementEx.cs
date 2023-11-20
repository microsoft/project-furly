// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace System.Xml
{
    using System.Runtime.Serialization;

    /// <summary>
    /// Xml element extensions
    /// </summary>
    public static class XmlElementEx
    {
        /// <summary>
        /// Serialize object to xml element
        /// </summary>
        /// <param name="o"></param>
        public static XmlElement? SerializeObject(object o)
        {
            var doc = new XmlDocument();
            var nav = doc.CreateNavigator();
            if (nav is null)
            {
                return null;
            }
            using (var writer = nav.AppendChild())
            {
                new DataContractSerializer(o.GetType()).WriteObject(writer, o);
            }
            return doc.DocumentElement;
        }
    }
}
