// <copyright>
// Copyright by Barefoot Church
//
// Licensed under the Rock Community License (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.rockrms.com/license
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using Rock.Plugin;

namespace com.barefootchurch.GroupMe.Migrations
{
    [MigrationNumber( 1, "1.9.0" )]
    public class AddGlobalAttribute : Migration
    {
        /// <summary>
        /// The commands to run to migrate plugin to the specific version
        /// </summary>
        public override void Up()
        {
            RockMigrationHelper.AddGlobalAttribute( Rock.SystemGuid.FieldType.TEXT, null, null, "GroupMe API Token", "You can find this on https://dev.groupme.com/applications", 0, string.Empty, SystemGuid.GlobalAttribute.GROUPME_API_TOKEN, SystemKey.GlobalAttribute.GROUPME_API_TOKEN );
        }

        /// <summary>
        /// The commands to undo a migration from a specific version
        /// </summary>
        public override void Down()
        {
            RockMigrationHelper.DeleteAttribute( SystemGuid.GlobalAttribute.GROUPME_API_TOKEN );
        }
    }
}