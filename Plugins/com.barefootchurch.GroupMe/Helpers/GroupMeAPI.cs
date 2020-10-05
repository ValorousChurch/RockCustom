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

using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;

using Rock;

namespace com.barefootchurch.GroupMe.Helpers
{
    internal class GroupMeAPI
    {
        private const string GROUPME_BASE_URL = "https://api.groupme.com/v3";
        private readonly RestClient _restClient;
        private readonly string _accessToken;

        public GroupMeAPI(string accesstoken)
        {
            _accessToken = accesstoken;
            _restClient = new RestClient( GROUPME_BASE_URL );
            _restClient.AddDefaultParameter( "token", _accessToken, ParameterType.QueryString );
        }
        /// <summary>
        /// Get the <see cref="GroupMeGroup"/> for the provided Id
        /// </summary>
        /// <param name="groupId">Id of the group to get</param>
        /// <returns>A <see cref="GroupMeGroup"/></returns>
        public GroupMeGroup GetGroup(int groupId)
        {
            var request = new RestRequest( $"groups/{ groupId }", Method.GET );
            var response = _restClient.Execute( request );
            
            if ( response.ErrorException != null )
            {
                throw response.ErrorException;
            }

            if ( response.StatusCode == HttpStatusCode.OK )
            {
                var responseJObject = JObject.Parse( response.Content );
                var group = responseJObject.SelectToken( "response" ).ToObject<GroupMeGroup>();
                return group;
            }

            return null;
        }

        /// <summary>
        /// Add the specified person to a group in GroupMe
        /// </summary>
        /// <param name="group">The <see cref="GroupMeGroup"/> to add them to</param>
        /// <param name="member">The <see cref="GroupMeGroupMemberAdd"/> to add to the group</param>
        /// <param name="memberId">The Id of the added member, if they are successfully added</param>
        public bool AddGroupMember(GroupMeGroup group, GroupMeGroupMemberAdd member, out int memberId)
        {
            memberId = 0;
            var memberList = new List<GroupMeGroupMemberAdd>();
            memberList.Add( member );
            var memberWrapper = new { members = memberList };
            var addRequest = new RestRequest( $"groups/{ group.Id }/members/add", Method.POST );
            addRequest.RequestFormat = DataFormat.Json;
            addRequest.AddParameter( "application/json", JObject.FromObject( memberWrapper ), ParameterType.RequestBody );
            var addResponse = _restClient.Execute( addRequest );
            
            if ( addResponse.ErrorException != null )
            {
                throw addResponse.ErrorException;
            }
            
            if ( addResponse.StatusCode == HttpStatusCode.Accepted )
            {
                var addResponseObject = (dynamic)JObject.Parse( addResponse.Content );
                var resultsId = addResponseObject.response.results_id;

                var resultRequest = new RestRequest( $"groups/{ group.Id }/members/results/{ resultsId }", Method.GET );
                var resultResponse = _restClient.Execute( resultRequest );
               
                // According to GroupMe's API docs, 503 = try later
                while ( resultResponse.StatusCode == HttpStatusCode.ServiceUnavailable )
                {
                    Task.Delay( 500 ).Wait();
                    resultResponse = _restClient.Execute( resultRequest );
                }

                if ( resultResponse.StatusCode == HttpStatusCode.OK )
                {
                    var resultResponseObject = JObject.Parse( resultResponse.Content );
                    var addedGroupMember = resultResponseObject.SelectToken( "response.members[0]" ).ToObject<GroupMeGroupMember>();
                    memberId = addedGroupMember.Id.AsInteger();

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Removes the specified member from their group in GroupMe
        /// </summary>
        /// <param name="group">The <see cref="GroupMeGroup"/> to remove them from</param>
        /// <param name="groupMember">The <see cref="GroupMeGroupMember"/> to remove</param>
        public bool RemoveGroupMember(GroupMeGroup group, GroupMeGroupMember groupMember)
        {
            var request = new RestRequest( $"groups/{ group.Id }/members/{ groupMember.Id }/remove", Method.POST );
            var response = _restClient.Execute( request );
            
            if ( response.ErrorException != null )
            {
                throw response.ErrorException;
            }
            
            if ( response.StatusCode == HttpStatusCode.OK )
            {
                return true;
            }

            return false;
        }
    }

    internal class GroupMeGroup
    {
        /// <summary>
        /// The id of the group.
        /// </summary>
        [JsonProperty( PropertyName = "id" )]
        public string Id { get; set; }

        /// <summary>
        /// The list of currently active group-members.
        /// </summary>
        [JsonProperty( PropertyName = "members" )]
        public IEnumerable<GroupMeGroupMember> Members { get; set; }

        /// <summary>
        /// The name of the group.
        /// </summary>
        [JsonProperty( PropertyName = "name" )]
        public string Name { get; set; }

        /// <summary>
        /// The share url of this group.
        /// </summary>
        [JsonProperty( PropertyName = "response.share_url" )]
        public string ShareUrl { get; set; }
    }

    internal class GroupMeGroupMember
    {
        /// <summary>
        /// The id of this user in the given group (this is a changing id from group to group, the UserId member shows the unique user id).
        /// </summary>
        [JsonProperty( PropertyName = "id" )]
        public string Id { get; set; }

        /// <summary>
        /// The nickname of the user in the given group.
        /// </summary>
        [JsonProperty( PropertyName = "nickname" )]
        public string Nickname { get; set; }

        /// <summary>
        /// The globally unique user id.
        /// </summary>
        [JsonProperty( PropertyName = "user_id" )]
        public string UserId { get; set; }
    }

    internal class GroupMeGroupMemberAdd
    {
        /// <summary>
        /// The email of the member to add.
        /// </summary>
        [JsonProperty( PropertyName = "email", NullValueHandling = NullValueHandling.Ignore )]
        public string Email { get; set; }

        /// <summary>
        /// The nickname of the member to add.
        /// </summary>
        [JsonProperty( PropertyName = "nickname", NullValueHandling = NullValueHandling.Ignore )]
        public string Nickname { get; set; }

        /// <summary>
        /// The phone number of the member to add.
        /// </summary>
        [JsonProperty( PropertyName = "phone_number", NullValueHandling = NullValueHandling.Ignore )]
        public string PhoneNumber { get; set; }

        /// <summary>
        /// The user id of the member to add.
        /// </summary>
        [JsonProperty( PropertyName = "user_id", NullValueHandling = NullValueHandling.Ignore )]
        public string UserId { get; set; }

        /// <summary>
        /// The Guid of the member to add
        /// </summary>
        [JsonProperty( PropertyName = "Guid", NullValueHandling = NullValueHandling.Ignore )]
        public string Guid { get; set; }
    }
}