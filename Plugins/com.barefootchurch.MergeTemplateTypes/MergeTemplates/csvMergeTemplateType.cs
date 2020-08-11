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
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Dynamic;
using System.IO;
using System.Linq;

using CsvHelper; // https://github.com/JoshClose/CsvHelper [Apache 2.0 license]

using Rock;
using Rock.Data;
using Rock.MergeTemplates;
using Rock.Model;

namespace com.barefootchurch.MergeTemplates
{
    [System.ComponentModel.Description( "A CSV File merge template" )]
    [Export( typeof( MergeTemplateType ) )]
    [ExportMetadata( "ComponentName", "CSV File" )]
    public class CsvMergeTemplateType : MergeTemplateType
    {
        /// <summary>
        /// Gets the attribute value defaults.
        /// </summary>
        /// <value>
        /// The attribute defaults.
        /// </value>
        public override Dictionary<string, string> AttributeValueDefaults
        {
            get
            {
                var defaults = new Dictionary<string, string>()
                {
                    { "Active", "True" }
                };
                return defaults;
            }
        }

        /// <summary>
        /// Gets the supported file extensions
        /// Returns NULL if the file extension doesn't matter or doesn't apply
        /// Rock will use this to warn the user if the file extension isn't supported
        /// </summary>
        /// <value>
        /// The supported file extensions.
        /// </value>
        public override IEnumerable<string> SupportedFileExtensions
        {
            get
            {
                return new string[] { "csv" };
            }
        }

        /// <summary>
        /// Creates the document.
        /// </summary>
        /// <param name="mergeTemplate">The merge template.</param>
        /// <param name="mergeObjectList">The list of merge objects (rows).</param>
        /// <param name="globalMergeFields">The global merge fields.</param>
        /// <returns></returns>
        public override BinaryFile CreateDocument(MergeTemplate mergeTemplate, List<object> mergeObjectList, Dictionary<string, object> globalMergeFields)
        {
            this.Exceptions = new List<Exception>();

            var rockContext = new RockContext();

            var binaryFileTypeService = new BinaryFileTypeService( rockContext );
            int defaultFileTypeId = binaryFileTypeService.Get( Rock.SystemGuid.BinaryFiletype.DEFAULT.AsGuid() ).Id;

            var binaryFileService = new BinaryFileService( rockContext );
            var outputBinaryFile = new BinaryFile();

            int recordCount = mergeObjectList.Count();

            IDictionary<string, object> template;
            var results = new List<object>();

            // Load the template file
            BinaryFile templateBinaryFile = binaryFileService.Get( mergeTemplate.TemplateBinaryFileId );
            if ( templateBinaryFile == null )
            {
                return null;
            }

            using ( var templateStream = new StreamReader( templateBinaryFile.ContentStream ) )
            using ( var csv = new CsvReader( templateStream ) )
            {
                // Read the headers
                csv.Read();
                csv.ReadHeader();

                // Read the first row of values
                csv.Read();
                template = csv.GetRecord<dynamic>();
            }

            // Merge each row
            for ( int recordIndex = 0; recordIndex < recordCount; recordIndex++ )
            {
                var result = new ExpandoObject() as IDictionary<string, object>;
                var mergeObjects = GetMergeObjects( mergeObjectList, globalMergeFields, recordIndex );

                // Merge each field in the row
                foreach ( var field in template )
                {
                    result.Add( field.Key, field.Value.ToString().ResolveMergeFields( mergeObjects ) );
                }

                results.Add( result );
            }

            using ( var outputStream = new MemoryStream() )
            using ( var outputWriter = new StreamWriter( outputStream ) )
            using ( var csv = new CsvWriter( outputWriter ) )
            {
                csv.WriteRecords( results );

                outputWriter.Flush();

                outputBinaryFile.IsTemporary = true;
                outputBinaryFile.ContentStream = outputStream;
                outputBinaryFile.FileName = "MergeTemplateOutput" + Path.GetExtension( templateBinaryFile.FileName );
                outputBinaryFile.MimeType = templateBinaryFile.MimeType;
                outputBinaryFile.BinaryFileTypeId = defaultFileTypeId;

                binaryFileService.Add( outputBinaryFile );
                rockContext.SaveChanges();
            }

            return outputBinaryFile;
        }

        /// <summary>
        /// Gets the merge objects.
        /// </summary>
        /// <param name="mergeObjectList">The merge object list.</param>
        /// <param name="globalMergeFields">The global merge fields.</param>
        /// <returns></returns>
        private static DotLiquid.Hash GetMergeObjects(List<object> mergeObjectList, Dictionary<string, object> globalMergeFields, int currentRecordIndex)
        {
            DotLiquid.Hash mergeObjects = new DotLiquid.Hash()
            {
                { "Row", mergeObjectList[currentRecordIndex] }
            };

            foreach ( var field in globalMergeFields )
            {
                mergeObjects.Add( field.Key, field.Value );
            }

            return mergeObjects;
        }

        /// <summary>
        /// Gets or sets the exceptions.
        /// </summary>
        /// <value>
        /// The exceptions.
        /// </value>
        public override List<Exception> Exceptions { get; set; }
    }
}
