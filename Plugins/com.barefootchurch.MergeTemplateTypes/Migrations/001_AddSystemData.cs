// <copyright>
// Copyright 2019 by Barefoot Church
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>
//
using Rock.Plugin;

namespace com.barefootchurch.MergeTemplates.Migrations
{
    [MigrationNumber( 1, "1.8.0" )] // TODO: Test and find minimum version
    public class AddSystemData : Migration
    {
        /// <summary>
        /// The commands to run to migrate plugin to the specific version
        /// </summary>
        public override void Up()
        {
            // Add Entity Type
            RockMigrationHelper.UpdateEntityType( "com.barefootchurch.MergeTemplates.CsvMergeTemplateType", "CSV Merge Template", "com.barefootchurch.MergeTemplates", false, true, SystemGuid.EntityType.CSV_MERGE_TEMPLATE );
        }

        /// <summary>
        /// The commands to undo a migration from a specific version
        /// </summary>
        public override void Down()
        {
            RockMigrationHelper.DeleteAttributesByEntityType( SystemGuid.EntityType.CSV_MERGE_TEMPLATE );
            RockMigrationHelper.DeleteEntityType( SystemGuid.EntityType.CSV_MERGE_TEMPLATE );
        }
    }
}