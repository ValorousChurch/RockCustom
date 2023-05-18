using System.Linq;

using Rock.Attribute;
using Rock.Data;
using Rock.Model;
using Rock.Jobs;
using Rock.Web.Cache;
using Rock.Web.UI.Controls;

namespace com.bricksandmortarstudio.FixCombinedGivers
{
    [SlidingDateRangeField( "Date Range", "Apply to Person records created within this time frame. If not set then all records are considered.", false, "", enabledSlidingDateRangeTypes: "Previous, Last, Current, DateRange" )]
    public class FixCombinedGivers : RockJob
    {
        public override void Execute()
        {
            RockContext rockContext = null;
            IQueryable<GroupMember> familyMembers = null;
            var dateRange = SlidingDateRangePicker.CalculateDateRangeFromDelimitedValues( GetAttributeValue( "DateRange" ) ?? "-1||" );
            int peopleUpdated = 0;
            var familyGroupType = GroupTypeCache.Get( Rock.SystemGuid.GroupType.GROUPTYPE_FAMILY );

            do
            {
                if ( familyMembers != null )
                {
                    foreach ( var familyMember in familyMembers.OrderBy( f => f.Id ).Take( 100 ).ToList() )
                    {
                        familyMember.Person.GivingGroupId = familyMember.GroupId;
                        peopleUpdated += 1;
                    }

                    rockContext.SaveChanges();
                }

                rockContext = new RockContext();

                familyMembers = new GroupMemberService( rockContext )
                    .Queryable( "Group,Person" )
                    .Where( g => g.Group.GroupType.Id == familyGroupType.Id &&
                          ( g.Person.GivingGroupId == 0 || g.Person.GivingGroupId == null ) );

                if ( dateRange.Start.HasValue )
                {
                    familyMembers = familyMembers.Where( g => g.Person.CreatedDateTime >= dateRange.Start );
                }

                if ( dateRange.End.HasValue )
                {
                    familyMembers = familyMembers.Where( g => g.Person.CreatedDateTime < dateRange.End );
                }
            } while ( familyMembers.Any() );

            this.Result = string.Format( "Combined giving on {0} {1}", peopleUpdated, peopleUpdated == 1 ? "person" : "people" );
        }
    }
}
