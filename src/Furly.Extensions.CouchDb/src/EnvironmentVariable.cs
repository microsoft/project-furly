// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.CouchDb
{
    /// <summary>
    /// Common runtime environment variables
    /// </summary>
    public static class EnvironmentVariable
    {
        /// <summary> Couch DB host name </summary>
        public const string COUCHDBHOSTNAME =
            "COUCHDB_HOSTNAME";
        /// <summary> Couch DB password </summary>
        public const string COUCHDBUSERNAME =
            "COUCHDB_USERNAME";
        /// <summary> Couch DBq secret </summary>
        public const string COUCHDBKEY =
            "COUCHDB_KEY";
    }
}
