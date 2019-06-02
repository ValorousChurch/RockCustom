// <copyright>
// Copyright by the Spark Development Network
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
//

/*
Origional source:
  - https://github.com/CentralAZ/Rock-CentralAZ/blob/9694ff5ca35573b8f1073f103cbd763760d33e68/RockWeb/Plugins/com_centralaz/Crm/FirstTimeGuestEntry.ascx
  - https://github.com/CentralAZ/Rock-CentralAZ/blob/9694ff5ca35573b8f1073f103cbd763760d33e68/RockWeb/Plugins/com_centralaz/Crm/FirstTimeGuestEntry.ascx.cs
*/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Web.UI;
using System.Web.UI.WebControls;

using Rock;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;
using Rock.Web.UI.Controls;
using Rock.Attribute;
using System.Text;

namespace RockWeb.Plugins.com_barefootchurch
{
    [DisplayName( "Connect Card Entry" )]
    [Category( "Barefoot Church" )]
    [Description( "A block for rapidly adding new people and adding them to a connection request which can launch a workflow to perform additional processing." )]

    // Connection Request Settings
    [ConnectionOpportunityField( "Connection Opportunity", "The connection opportunity that new requests will be made for.", true, "", false, "Connection Request Settings", 0 )]
    [TextField( "Guest Type", "A comma-delimited list of different guest types that can be selected.  The selected item will be added to the Comment field of the connection request.", true, "Returning Guest, First Time Guest - Local, First Time Guest - Vacationer", "Connection Request Settings", 1 )]
    [TextField( "Decisions", "A comma-delimited list of different options that can be checked.  These will be added to the Comment field of the connection request.", true, "Salvation, Recommit", "Connection Request Settings", 2 )]
    [TextField( "Interests", "A comma-delimited list of different options that can be checked.  These will be added to the Comment field of the connection request.", true, "Baptism, Volunteering, Joining a Group, Leading a Group, Partnering", "Connection Request Settings", 3 )]
    [TextField( "Entry Source", "A comma-delimited list of places where the data entry can occur. The selected item will be added to the Comment field of the connection request.", true, "Weekend, Kids World, Unleashed, Other", "Connection Request Settings", 4 )]

    // Person Settings
    [DefinedValueField( "2E6540EA-63F0-40FE-BE50-F2A84735E600", "Connection Status", "The connection status to use for new individuals (default: 'Visitor'.)", true, false, Rock.SystemGuid.DefinedValue.PERSON_CONNECTION_STATUS_VISITOR, "Person Settings", 5 )]
    [DefinedValueField( "8522BADD-2871-45A5-81DD-C76DA07E2E7E", "Record Status", "The record status to use for new individuals (default: 'Pending'.)", true, false, "283999EC-7346-42E3-B807-BCE9B2BABB49", "Person Settings", 6 )]
    [BooleanField( "Is Sms Checked By Default ", "Is the 'Enable SMS' option checked by default.", true, "Person Settings", 7, "IsSmsChecked" )]

    // Child Settings
    [DefinedValueField( "2E6540EA-63F0-40FE-BE50-F2A84735E600", "Child Connection Status", "The connection status to use for new children (default: 'Visitor'.)", true, false, Rock.SystemGuid.DefinedValue.PERSON_CONNECTION_STATUS_VISITOR, "Child Settings", 8 )]

    //Prayer Request Settings
    [BooleanField( "Is Prayer Request Enabled", "Is the Prayer Request text box visible.", true, "Prayer Request Settings", 9 )]
    [CategoryField( "Prayer Category", "The  category to use for all new prayer requests.", false, "Rock.Model.PrayerRequest", "", "", false, "4B2D88F5-6E45-4B4B-8776-11118C8E8269", "Prayer Request Settings", 10, "PrayerCategory" )]

    // Misc
    [LinkedPage( "Person Profile Page", "The person profile page.", false, "", "", 11 )]

    public partial class ConnectCardEntry : Rock.Web.UI.RockBlock
    {
        #region Fields

        ConnectionOpportunity _connectionOpportunity = null;
        RockContext _rockContext = null;
        DefinedValueCache _dvcConnectionStatus = null;
        DefinedValueCache _dvcChildConnectionStatus = null;
        DefinedValueCache _dvcRecordStatus = null;
        DefinedValueCache _single = null;
        DefinedValueCache _married = null;
        DefinedValueCache _homeAddressType = null;
        GroupTypeCache _familyType = null;
        GroupTypeRoleCache _adultRole = null;
        GroupTypeRoleCache _childRole = null;
        bool _isValidSettings = true;
        bool _isPrayerRequestEnabled = false;
        int? _personProfilePage;

        private const string CAMPUS_SETTING = "FirstTimeGuestEntry_SelectedCampus";
        private const string SOURCE_SETTING = "FirstTimeGuestEntry_SelectedSource";

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the child members that have been added by user
        /// </summary>
        /// <value>
        /// The group members.
        /// </value>
        protected List<PreRegistrationChild> Children { get; set; }

        #endregion

        #region Base Control Methods

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Init" /> event.
        /// </summary>
        /// <param name="e">An <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnInit( EventArgs e )
        {
            base.OnInit( e );

            // this event gets fired after block settings are updated. it's nice to repaint the screen if these settings would alter it
            this.BlockUpdated += Block_BlockUpdated;
            this.AddConfigurationUpdateTrigger( upnlContent );
        }

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Load" /> event.
        /// </summary>
        /// <param name="e">The <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnLoad( EventArgs e )
        {
            base.OnLoad( e );

            Guid? personProfilePageGuid = GetAttributeValue( "PersonProfilePage" ).AsGuidOrNull();
            if ( personProfilePageGuid != null )
            {
                _personProfilePage = PageCache.Get( personProfilePageGuid.Value ).Id;
            }


            if ( !CheckSettings() )
            {
                _isValidSettings = false;
                nbNotice.Visible = true;
                pnlView.Visible = false;
            }
            else
            {
                nbNotice.Visible = false;
                pnlView.Visible = true;

                if ( !Page.IsPostBack )
                {
                    // Build the dynamic children controls
                    Children = new List<PreRegistrationChild>();
                    CreateChildrenControls( true );

                    ShowDetail();
                }
                else
                {
                    GetChildrenData();
                }
            }
        }

        /// <summary>
        /// Restores the view-state information from a previous user control request that was saved by the <see cref="M:System.Web.UI.UserControl.SaveViewState" /> method.
        /// </summary>
        /// <param name="savedState">An <see cref="T:System.Object" /> that represents the user control state to be restored.</param>
        protected override void LoadViewState( object savedState )
        {
            base.LoadViewState( savedState );

            Children = ViewState["Children"] as List<PreRegistrationChild> ?? new List<PreRegistrationChild>();
            CreateChildrenControls( false );
        }

        /// <summary>
        /// Saves any user control view-state changes that have occurred since the last page postback.
        /// </summary>
        /// <returns>
        /// Returns the user control's current view state. If there is no view state associated with the control, it returns null.
        /// </returns>
        protected override object SaveViewState()
        {
            ViewState["Children"] = Children;

            return base.SaveViewState();
        }

        #endregion

        #region Events

        /// <summary>
        /// Handles the BlockUpdated event of the control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void Block_BlockUpdated( object sender, EventArgs e )
        {
            ShowDetail();
        }

        /// <summary>
        /// Handles the Click event of the btnCancel control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void btnCancel_Click( object sender, EventArgs e )
        {
            NavigateToParentPage();
        }

        /// <summary>
        /// Handles the Click event of the btnSave control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void btnSave_Click( object sender, EventArgs e )
        {
            if ( Page.IsValid & _isValidSettings )
            {
                var rockContext = new RockContext();
                var personService = new PersonService( rockContext );

                Person firstAdult = null;
                Person secondAdult = null;
                Group family = null;
                GroupLocation homeLocation = null;
                bool isMatch = false;

                var firstAdultChanges = new History.HistoryChangeList();
                var secondAdultChanges = new History.HistoryChangeList();
                var familyChanges = new History.HistoryChangeList();

                var addedPeopleNames = new List<string>();

                // First try to grab the person from the picker
                if ( ppGuest.PersonId != null )
                {
                    firstAdult = new PersonService( rockContext ).Get( ppGuest.PersonId.Value );
                }

                if ( pnlNewPerson.Enabled )
                {
                    if ( firstAdult == null )
                    {
                        // Try to find person by name/email
                        var matches = personService.FindPersons( tbFirstName.Text.Trim(), tbLastName.Text.Trim(), tbEmail.Text.Trim() );
                        if ( matches.Count() == 1 )
                        {
                            firstAdult = matches.First();
                            isMatch = true;
                        }
                    }

                    // Check to see if this is a new person
                    if ( firstAdult == null )
                    {
                        // If so, create the person and family record for the new person
                        firstAdult = new Person();
                        firstAdult.FirstName = tbFirstName.Text.Trim();
                        firstAdult.LastName = tbLastName.Text.Trim();
                        firstAdult.Email = tbEmail.Text.Trim();
                        firstAdult.IsEmailActive = true;
                        firstAdult.EmailPreference = EmailPreference.EmailAllowed;
                        firstAdult.RecordTypeValueId = DefinedValueCache.Get( Rock.SystemGuid.DefinedValue.PERSON_RECORD_TYPE_PERSON.AsGuid() ).Id;
                        firstAdult.ConnectionStatusValueId = _dvcConnectionStatus.Id;
                        firstAdult.RecordStatusValueId = _dvcRecordStatus.Id;
                        firstAdult.Gender = rblGender.SelectedValueAsEnum<Gender>( Gender.Unknown );

                        // set SourceofVisit
                        if ( dvpVisitSource.SelectedDefinedValueId.GetValueOrDefault() > 0 )
                        {
                            firstAdult.LoadAttributes();
                            firstAdult.SetAttributeValue( "SourceofVisit", DefinedValueCache.Get( dvpVisitSource.SelectedDefinedValueId.GetValueOrDefault(), rockContext ).Guid );
                            firstAdult.SaveAttributeValues();
                        }

                        family = PersonService.SaveNewPerson( firstAdult, rockContext, cpCampus.SelectedCampusId, false );
                    }
                }

                if ( firstAdult != null )
                {
                    addedPeopleNames.Add( firstAdult.FullName );

                    History.EvaluateChange( firstAdultChanges, "Connection Status", firstAdult.ConnectionStatusValueId, _dvcConnectionStatus.Id );
                    firstAdult.ConnectionStatusValueId = _dvcConnectionStatus.Id;

                    // Get the current person's families
                    var families = firstAdult.GetFamilies( rockContext );

                    // If address can being entered, look for first family with a home location
                    foreach ( var aFamily in families )
                    {
                        homeLocation = aFamily.GroupLocations
                            .Where( l =>
                                l.GroupLocationTypeValueId == _homeAddressType.Id &&
                                l.IsMappedLocation )
                            .FirstOrDefault();
                        if ( homeLocation != null )
                        {
                            family = aFamily;
                            break;
                        }
                    }

                    // If a family wasn't found with a home location, use the person's first family
                    if ( family == null )
                    {
                        family = families.FirstOrDefault();
                    }

                    History.EvaluateChange( firstAdultChanges, "Campus", family.CampusId, cpCampus.SelectedCampusId );
                    family.CampusId = cpCampus.SelectedCampusId;

                    if ( pnlNewPerson.Enabled )
                    {
                        // Save the contact info
                        History.EvaluateChange( firstAdultChanges, "Email", firstAdult.Email, tbEmail.Text );
                        firstAdult.Email = tbEmail.Text;

                        if ( !isMatch || !string.IsNullOrWhiteSpace( pnHome.Number ) )
                        {
                            SetPhoneNumber( rockContext, firstAdult, pnHome, null, Rock.SystemGuid.DefinedValue.PERSON_PHONE_TYPE_HOME.AsGuid(), firstAdultChanges );
                        }
                        if ( !isMatch || !string.IsNullOrWhiteSpace( pnCell.Number ) )
                        {
                            SetPhoneNumber( rockContext, firstAdult, pnCell, cbSms, Rock.SystemGuid.DefinedValue.PERSON_PHONE_TYPE_MOBILE.AsGuid(), firstAdultChanges );
                        }

                        if ( !isMatch || !string.IsNullOrWhiteSpace( acAddress.Street1 ) )
                        {
                            string oldLocation = homeLocation != null ? homeLocation.Location.ToString() : string.Empty;
                            string newLocation = string.Empty;

                            var location = new LocationService( rockContext ).Get( acAddress.Street1, acAddress.Street2, acAddress.City, acAddress.State, acAddress.PostalCode, acAddress.Country );
                            if ( location != null )
                            {
                                if ( homeLocation == null )
                                {
                                    homeLocation = new GroupLocation();
                                    homeLocation.GroupLocationTypeValueId = _homeAddressType.Id;
                                    family.GroupLocations.Add( homeLocation );
                                }
                                else
                                {
                                    oldLocation = homeLocation.Location.ToString();
                                }

                                homeLocation.Location = location;
                                newLocation = location.ToString();
                            }
                            else
                            {
                                if ( homeLocation != null )
                                {
                                    homeLocation.Location = null;
                                    family.GroupLocations.Remove( homeLocation );
                                    new GroupLocationService( rockContext ).Delete( homeLocation );
                                }
                            }

                            History.EvaluateChange( familyChanges, "Home Location", oldLocation, newLocation );
                        }

                        // Check for the second adult
                        if ( !string.IsNullOrWhiteSpace( tbSecondAdultFirstName.Text ) )
                        {
                            secondAdult = firstAdult.GetSpouse( rockContext );
                            bool isSpouseMatch = true;

                            if ( secondAdult == null ||
                                !tbSecondAdultFirstName.Text.Trim().Equals( secondAdult.FirstName.Trim(), StringComparison.OrdinalIgnoreCase ) ||
                                !tbSecondAdultLastName.Text.Trim().Equals( secondAdult.LastName.Trim(), StringComparison.OrdinalIgnoreCase ) )
                            {
                                secondAdult = new Person();
                                isSpouseMatch = false;

                                secondAdult.FirstName = tbSecondAdultFirstName.Text.FixCase();
                                History.EvaluateChange( secondAdultChanges, "First Name", string.Empty, secondAdult.FirstName );

                                secondAdult.LastName = tbSecondAdultLastName.Text.FixCase();
                                if ( secondAdult.LastName.IsNullOrWhiteSpace() )
                                {
                                    secondAdult.LastName = firstAdult.LastName;
                                }
                                History.EvaluateChange( secondAdultChanges, "Last Name", string.Empty, secondAdult.LastName );

                                secondAdult.RecordTypeValueId = DefinedValueCache.Get( Rock.SystemGuid.DefinedValue.PERSON_RECORD_TYPE_PERSON.AsGuid() ).Id;
                                secondAdult.ConnectionStatusValueId = _dvcConnectionStatus.Id;
                                secondAdult.RecordStatusValueId = _dvcRecordStatus.Id;
                                secondAdult.Gender = rblSecondAdultGender.SelectedValueAsEnum<Gender>( Gender.Unknown );

                                secondAdult.IsEmailActive = true;
                                secondAdult.EmailPreference = EmailPreference.EmailAllowed;

                                var groupMember = new GroupMember();
                                groupMember.GroupRoleId = _adultRole.Id;
                                groupMember.Person = secondAdult;

                                family.Members.Add( groupMember );

                                // set marital status
                                bool? isMarried = rbMarried.SelectedValue.AsBooleanOrNull();
                                if ( isMarried.HasValue && isMarried.Value )
                                {
                                    secondAdult.MaritalStatusValueId = _married.Id;
                                    firstAdult.MaritalStatusValueId = _married.Id;
                                }
                                else
                                {
                                    secondAdult.MaritalStatusValueId = _single.Id;
                                    firstAdult.MaritalStatusValueId = _single.Id;
                                }

                            }

                            History.EvaluateChange( secondAdultChanges, "Email", secondAdult.Email, tbSecondAdultEmail.Text );
                            secondAdult.Email = tbSecondAdultEmail.Text;

                            History.EvaluateChange( secondAdultChanges, "Connection Status", secondAdult.ConnectionStatusValueId, _dvcConnectionStatus.Id );
                            secondAdult.ConnectionStatusValueId = _dvcConnectionStatus.Id;

                            if ( !isSpouseMatch || !string.IsNullOrWhiteSpace( pnHome.Number ) )
                            {
                                SetPhoneNumber( rockContext, secondAdult, pnHome, null, Rock.SystemGuid.DefinedValue.PERSON_PHONE_TYPE_HOME.AsGuid(), secondAdultChanges );
                            }

                            if ( !isSpouseMatch || !string.IsNullOrWhiteSpace( pnSecondAdultCell.Number ) )
                            {
                                SetPhoneNumber( rockContext, secondAdult, pnSecondAdultCell, cbSecondAdultSms, Rock.SystemGuid.DefinedValue.PERSON_PHONE_TYPE_MOBILE.AsGuid(), secondAdultChanges );
                            }
                        }

                        // Add Children
                        foreach ( var child in Children )
                        {
                            Person childPerson = personService.Get( child.Guid );


                            // If person was not found, Look for existing person in same family with same name and birthdate
                            if ( firstAdult == null && child.BirthDate.HasValue )
                            {
                                var possibleMatch = new Person { NickName = child.NickName, LastName = child.LastName };
                                possibleMatch.SetBirthDate( child.BirthDate );
                                firstAdult = family.MatchingFamilyMember( possibleMatch );
                            }

                            // Create a new person
                            if ( childPerson == null )
                            {
                                childPerson = new Person();
                                personService.Add( childPerson );

                                childPerson.Guid = child.Guid;
                                childPerson.FirstName = child.NickName.FixCase();
                                childPerson.LastName = child.LastName.FixCase();
                                childPerson.RecordTypeValueId = DefinedValueCache.Get( Rock.SystemGuid.DefinedValue.PERSON_RECORD_TYPE_PERSON.AsGuid() ).Id;
                                childPerson.RecordStatusValueId = _dvcRecordStatus.Id;
                                childPerson.ConnectionStatusValueId = _dvcChildConnectionStatus.Id;

                                if ( child.Gender != Gender.Unknown )
                                {
                                    childPerson.Gender = child.Gender;
                                }

                                if ( child.BirthDate.HasValue )
                                {
                                    childPerson.SetBirthDate( child.BirthDate );
                                }

                                if ( child.GradeOffset.HasValue )
                                {
                                    childPerson.GradeOffset = child.GradeOffset;
                                }

                                var groupMember = new GroupMember();
                                groupMember.GroupRoleId = _childRole.Id;
                                groupMember.Person = childPerson;

                                family.Members.Add( groupMember );
                                addedPeopleNames.Add( childPerson.FirstName + " " + childPerson.LastName );
                            }
                        }
                    }
                }

                // Save the first adult/second adult/children and change history
                rockContext.SaveChanges();
                HistoryService.SaveChanges( rockContext, typeof( Person ),
                    Rock.SystemGuid.Category.HISTORY_PERSON_DEMOGRAPHIC_CHANGES.AsGuid(), firstAdult.Id, firstAdultChanges );
                HistoryService.SaveChanges( rockContext, typeof( Person ),
                    Rock.SystemGuid.Category.HISTORY_PERSON_FAMILY_CHANGES.AsGuid(), firstAdult.Id, familyChanges );
                if ( secondAdult != null )
                {
                    addedPeopleNames.Add( secondAdult.FullName );
                    HistoryService.SaveChanges( rockContext, typeof( Person ),
                        Rock.SystemGuid.Category.HISTORY_PERSON_DEMOGRAPHIC_CHANGES.AsGuid(), secondAdult.Id, secondAdultChanges );
                    HistoryService.SaveChanges( rockContext, typeof( Person ),
                        Rock.SystemGuid.Category.HISTORY_PERSON_FAMILY_CHANGES.AsGuid(), secondAdult.Id, familyChanges );
                }

                // Save the Connection Requests
                CreateConnectionRequest( rockContext, firstAdult );
                //CreateConnectionRequest( rockContext, secondAdult );

                // Save the Prayer Request
                if ( _isPrayerRequestEnabled )
                {
                    CreatePrayerRequest( rockContext, firstAdult );
                }

                // Reload page
                nbMessage.Text = string.Format( "New entry for {0} saved.", addedPeopleNames.AsDelimited( ", ", " and " ) );
                nbMessage.Visible = true;
                hfShowSuccess.Value = "true";
                ClearControls();
                ShowDetail();
            }
        }

        /// <summary>
        /// Handles the AddChildClick event of the prChildren control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void prChildren_AddChildClick( object sender, EventArgs e )
        {
            AddChild();
            CreateChildrenControls( true );
        }

        /// <summary>
        /// Handles the DeleteClick event of the ChildRow control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        /// <exception cref="NotImplementedException"></exception>
        private void ChildRow_DeleteClick( object sender, EventArgs e )
        {
            var row = sender as PreRegistrationChildRow;
            var child = Children.FirstOrDefault( m => m.Guid.Equals( row.PersonGuid ) );
            if ( child != null )
            {
                Children.Remove( child );
            }

            CreateChildrenControls( true );
        }

        /// <summary>
        /// Clears the controls.
        /// </summary>
        private void ClearControls()
        {
            ppGuest.PersonId = null;
            ppGuest.SetValue( null );
            tbFirstName.Text = tbLastName.Text = pnCell.Text = tbEmail.Text = pnHome.Text = string.Empty;
            tbSecondAdultFirstName.Text = tbSecondAdultLastName.Text = pnSecondAdultCell.Text = tbSecondAdultEmail.Text = string.Empty;
            tbComments.Text = tbPrayerRequests.Text = string.Empty;
            acAddress.Street1 = acAddress.Street2 = acAddress.City = acAddress.PostalCode = string.Empty;
            pnlNewPerson.Enabled = tbFirstName.Required = tbLastName.Required = true;

            Children = new List<PreRegistrationChild>();
            prChildren.ClearRows();
        }

        /// <summary>
        /// Creates the children controls.
        /// </summary>
        private void CreateChildrenControls( bool setSelection )
        {
            prChildren.ClearRows();

            foreach ( var child in Children )
            {
                if ( child != null )
                {
                    var childRow = new PreRegistrationChildRow();
                    childRow.ValidationGroup = this.BlockValidationGroup;

                    prChildren.Controls.Add( childRow );

                    childRow.DeleteClick += ChildRow_DeleteClick;
                    string childGuidString = child.Guid.ToString().Replace( "-", "_" );
                    childRow.ID = string.Format( "row_{0}", childGuidString );
                    childRow.PersonId = child.Id;
                    childRow.PersonGuid = child.Guid;

                    childRow.ShowSuffix = false;
                    childRow.ShowGender = true;
                    childRow.RequireGender = false;
                    childRow.ShowBirthDate = true;
                    childRow.RequireBirthDate = false;
                    childRow.ShowGrade = true;
                    childRow.RequireGrade = false;
                    childRow.ShowMobilePhone = false;
                    childRow.RequireMobilePhone = false;

                    var _relationshipTypes = new Dictionary<int, string>();
                    _relationshipTypes.Add( 0, "Child" );
                    childRow.RelationshipTypeList = _relationshipTypes;

                    // Hide relationship role since is it not needed.
                    var relationshipDropDown = childRow.FindControl( "_ddlRelationshipType" ) as RockDropDownList;
                    relationshipDropDown.Visible = false;

                    childRow.ValidationGroup = BlockValidationGroup;

                    if ( setSelection )
                    {
                        childRow.NickName = child.NickName;
                        childRow.LastName = child.LastName;
                        childRow.SuffixValueId = child.SuffixValueId;
                        childRow.Gender = child.Gender;
                        childRow.BirthDate = child.BirthDate;
                        childRow.GradeOffset = child.GradeOffset;
                        childRow.RelationshipType = child.RelationshipType;
                        childRow.MobilePhone = child.MobilePhoneNumber;
                        childRow.MobilePhoneCountryCode = child.MobileCountryCode;

                        childRow.SetAttributeValues( child );
                    }

                }
            }
        }


        /// <summary>
        /// Handles the SelectPerson event of the ppGuest control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void ppGuest_SelectPerson( object sender, EventArgs e )
        {
            if ( ppGuest.PersonId.HasValue )
            {
                pnlNewPerson.Enabled = tbFirstName.Required = tbLastName.Required = tbEmail.Required = false;

                Children = new List<PreRegistrationChild>();
                prChildren.ClearRows();

                if ( _personProfilePage.HasValue )
                {
                    lbPersonProfile.Visible = true;
                    var queryParams = new Dictionary<string, string>();
                    queryParams.Add( "PersonId", ppGuest.PersonId.ToString() );
                    lbPersonProfile.NavigateUrl = LinkedPageUrl( "PersonProfilePage", queryParams );
                }
            }
            else
            {
                pnlNewPerson.Enabled = tbFirstName.Required = tbLastName.Required = true;

                lbPersonProfile.Visible = false;
                lbPersonProfile.NavigateUrl = "";
            }
        }

        /// <summary>
        /// Handles the SelectedIndexChanged event of the rblSource control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void rblSource_SelectedIndexChanged( object sender, EventArgs e )
        {
            SetUserPreference( SOURCE_SETTING, rblSource.SelectedValue );
        }

        /// <summary>
        /// Handles the SelectedIndexChanged event of the cpCampus control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void cpCampus_SelectedIndexChanged( object sender, EventArgs e )
        {
            SetUserPreference( CAMPUS_SETTING, cpCampus.SelectedCampusId.ToString() );
        }

        #endregion

        #region Methods

        /// <summary>
        /// Shows the detail.
        /// </summary>
        private void ShowDetail()
        {
            // NOTE: Don't include Inactive Campuses for the cpCampus control
            cpCampus.Campuses = CampusCache.All( false );
            cpCampus.Items[0].Text = "";

            cpCampus.SelectedCampusId = GetUserPreference( CAMPUS_SETTING ).AsIntegerOrNull();
            if ( cpCampus.SelectedCampusId == null )
            {
                cpCampus.SelectedCampusId = CampusCache.All().First().Id;
            }

            // Set SMS Checkbox
            bool IsSmsChecked = GetAttributeValue( "IsSmsChecked" ).AsBoolean( true );
            cbSecondAdultSms.Checked = cbSms.Checked = IsSmsChecked;

            // Build the Guest Type radio button list...
            var guestTypeList = GetAttributeValue( "GuestType" ).SplitDelimitedValues( false );
            rblGuestType.Items.Clear();
            foreach ( var item in guestTypeList )
            {
                rblGuestType.Items.Add( new ListItem( item, item ) );
            }
            rblGuestType.DataBind();
            // First is the default
            rblGuestType.SelectedValue = guestTypeList[0];

            // Build Decisions list...
            var decisionList = GetAttributeValue( "Decisions" ).SplitDelimitedValues( false );
            cblDecisions.Items.Clear();
            foreach ( var decision in decisionList )
            {
                cblDecisions.Items.Add( new ListItem( decision, decision ) );
            }
            cblDecisions.DataBind();

            // Build Interests list...
            var interestList = GetAttributeValue( "Interests" ).SplitDelimitedValues( false );
            cblInterests.Items.Clear();
            foreach ( var interest in interestList )
            {
                cblInterests.Items.Add( new ListItem( interest, interest ) );
            }
            cblInterests.DataBind();

            // Build the Entry Source radio button list...
            var entrySourceList = GetAttributeValue( "EntrySource" ).SplitDelimitedValues( false );
            rblSource.Items.Clear();
            foreach ( var item in entrySourceList )
            {
                rblSource.Items.Add( new ListItem( item, item ) );
            }
            rblSource.DataBind();

            // Use the user's preference and set the Source Setting (if it is still an option in the list).
            var sourceSetting = GetUserPreference( SOURCE_SETTING ).ToStringSafe();
            if ( rblSource.Items.Contains( new ListItem( sourceSetting, sourceSetting ) ) )
            {
                rblSource.SelectedValue = sourceSetting;
            }

            tbPrayerRequests.Visible = _isPrayerRequestEnabled;
        }

        /// <summary>
        /// Checks the settings.
        /// </summary>
        /// <returns></returns>
        private bool CheckSettings()
        {
            _rockContext = _rockContext ?? new RockContext();

            var connectionOpportunityService = new ConnectionOpportunityService( _rockContext );

            Guid? connectionOpportunityGuid = GetAttributeValue( "ConnectionOpportunity" ).AsGuidOrNull();
            if ( connectionOpportunityGuid.HasValue )
            {
                _connectionOpportunity = connectionOpportunityService.Get( connectionOpportunityGuid.Value );
            }

            if ( _connectionOpportunity == null )
            {
                connectionOpportunityGuid = PageParameter( "ConnectionOpportunityGuid" ).AsGuidOrNull();
                if ( connectionOpportunityGuid.HasValue )
                {
                    _connectionOpportunity = connectionOpportunityService.Get( connectionOpportunityGuid.Value );
                }
            }

            if ( _connectionOpportunity == null )
            {
                int? connectionOpportunityId = PageParameter( "ConnectionOpportunityId" ).AsIntegerOrNull();
                if ( connectionOpportunityId.HasValue )
                {
                    _connectionOpportunity = connectionOpportunityService.Get( connectionOpportunityId.Value );
                }
            }

            if ( _connectionOpportunity == null )
            {
                nbNotice.Heading = "Missing Connection Opportunity Setting";
                nbNotice.Text = "<p>Please edit the block settings. This block requires a valid Connection Opportunity setting.</p>";
                return false;
            }

            _dvcConnectionStatus = DefinedValueCache.Get( GetAttributeValue( "ConnectionStatus" ).AsGuid() );
            if ( _dvcConnectionStatus == null )
            {
                nbNotice.Heading = "Invalid Connection Status";
                nbNotice.Text = "<p>The selected Connection Status setting does not exist.</p>";
                return false;
            }

            _dvcChildConnectionStatus = DefinedValueCache.Get( GetAttributeValue( "ChildConnectionStatus" ).AsGuid() );
            if ( _dvcChildConnectionStatus == null )
            {
                nbNotice.Heading = "Invalid Child Connection Status";
                nbNotice.Text = "<p>The selected Child Connection Status setting does not exist.</p>";
                return false;
            }

            _dvcRecordStatus = DefinedValueCache.Get( GetAttributeValue( "RecordStatus" ).AsGuid() );
            if ( _dvcRecordStatus == null )
            {
                nbNotice.Heading = "Invalid Record Status";
                nbNotice.Text = "<p>The selected Record Status setting does not exist.</p>";
                return false;
            }

            _single = DefinedValueCache.Get( Rock.SystemGuid.DefinedValue.PERSON_MARITAL_STATUS_SINGLE.AsGuid() );
            _married = DefinedValueCache.Get( Rock.SystemGuid.DefinedValue.PERSON_MARITAL_STATUS_MARRIED.AsGuid() );
            _homeAddressType = DefinedValueCache.Get( Rock.SystemGuid.DefinedValue.GROUP_LOCATION_TYPE_HOME.AsGuid() );
            _familyType = GroupTypeCache.Get( Rock.SystemGuid.GroupType.GROUPTYPE_FAMILY.AsGuid() );
            _adultRole = _familyType.Roles.FirstOrDefault( r => r.Guid.Equals( Rock.SystemGuid.GroupRole.GROUPROLE_FAMILY_MEMBER_ADULT.AsGuid() ) );
            _childRole = _familyType.Roles.FirstOrDefault( r => r.Guid.Equals( Rock.SystemGuid.GroupRole.GROUPROLE_FAMILY_MEMBER_CHILD.AsGuid() ) );

            if ( _single == null || _married == null || _homeAddressType == null || _familyType == null || _adultRole == null || _childRole == null )
            {
                nbNotice.Heading = "Missing System Value";
                nbNotice.Text = "<p>There is a missing or invalid system value. Check the settings for Marital Status of 'Single'/'Married', Location Type of 'Home', Group Type of 'Family', and Family Group Role of 'Adult'.</p>";
                return false;
            }

            _isPrayerRequestEnabled = GetAttributeValue( "IsPrayerRequestEnabled" ).AsBoolean();

            return true;
        }

        /// <summary>
        /// Sets the phone number.
        /// </summary>
        /// <param name="rockContext">The rock context.</param>
        /// <param name="person">The person.</param>
        /// <param name="pnbNumber">The PNB number.</param>
        /// <param name="cbSms">The cb SMS.</param>
        /// <param name="phoneTypeGuid">The phone type unique identifier.</param>
        /// <param name="changes">The changes.</param>
        private void SetPhoneNumber( RockContext rockContext, Person person, PhoneNumberBox pnbNumber, RockCheckBox cbSms, Guid phoneTypeGuid, History.HistoryChangeList changes )
        {
            var phoneType = DefinedValueCache.Get( phoneTypeGuid );
            if ( phoneType != null )
            {
                var phoneNumber = person.PhoneNumbers.FirstOrDefault( n => n.NumberTypeValueId == phoneType.Id );
                string oldPhoneNumber = string.Empty;
                if ( phoneNumber == null )
                {
                    phoneNumber = new PhoneNumber { NumberTypeValueId = phoneType.Id };
                }
                else
                {
                    oldPhoneNumber = phoneNumber.NumberFormattedWithCountryCode;
                }

                phoneNumber.CountryCode = PhoneNumber.CleanNumber( pnbNumber.CountryCode );
                phoneNumber.Number = PhoneNumber.CleanNumber( pnbNumber.Number );

                if ( string.IsNullOrWhiteSpace( phoneNumber.Number ) )
                {
                    if ( phoneNumber.Id > 0 )
                    {
                        new PhoneNumberService( rockContext ).Delete( phoneNumber );
                        person.PhoneNumbers.Remove( phoneNumber );
                    }
                }
                else
                {
                    if ( phoneNumber.Id <= 0 )
                    {
                        person.PhoneNumbers.Add( phoneNumber );
                    }
                    if ( cbSms != null && cbSms.Checked )
                    {
                        phoneNumber.IsMessagingEnabled = true;
                        person.PhoneNumbers
                            .Where( n => n.NumberTypeValueId != phoneType.Id )
                            .ToList()
                            .ForEach( n => n.IsMessagingEnabled = false );
                    }
                }

                History.EvaluateChange( changes,
                    string.Format( "{0} Phone", phoneType.Value ),
                    oldPhoneNumber, phoneNumber.NumberFormattedWithCountryCode );
            }
        }

        /// <summary>
        /// Creates the connection request.
        /// </summary>
        /// <param name="rockContext">The rock context.</param>
        /// <param name="person">The person.</param>
        private void CreateConnectionRequest( RockContext rockContext, Person person )
        {
            if ( person != null && _connectionOpportunity != null )
            {
                int defaultStatusId = _connectionOpportunity.ConnectionType.ConnectionStatuses
                                    .Where( s => s.IsDefault )
                                    .Select( s => s.Id )
                                    .FirstOrDefault();

                ConnectionRequestService connectionRequestService = new ConnectionRequestService( rockContext );
                ConnectionRequest connectionRequest = new ConnectionRequest();
                connectionRequest.PersonAliasId = person.PrimaryAliasId.Value;
                connectionRequest.ConnectionOpportunityId = _connectionOpportunity.Id;
                connectionRequest.ConnectionState = ConnectionState.Active;
                connectionRequest.ConnectionStatusId = defaultStatusId;
                connectionRequest.CampusId = cpCampus.SelectedCampusId;

                StringBuilder sb = new StringBuilder();

                sb.AppendFormat( "#### General\n" );
                sb.AppendFormat( "**Entry Point**: {0}  \n", rblSource.SelectedValue );
                sb.AppendFormat( "**Guest Type**: {0}  \n", rblGuestType.SelectedValue );

                if ( cblDecisions.SelectedValues.Count > 0 )
                {
                    sb.AppendFormat( "#### Decisions\n" );
                    sb.AppendFormat( "- {0}", cblDecisions.SelectedValues.AsDelimited( "\n- " ) );
                    sb.AppendFormat( "\n" );
                }

                if ( cblInterests.SelectedValues.Count > 0 )
                {
                    sb.AppendFormat( "#### Interests\n" );
                    sb.AppendFormat( "- {0}", cblInterests.SelectedValues.AsDelimited( "\n- " ) );
                    sb.AppendFormat( "\n" );
                }

                if ( !string.IsNullOrWhiteSpace( tbComments.Text ) )
                {
                    sb.AppendFormat( "#### Additional Comments\n" );
                    sb.AppendFormat( "{0}", tbComments.Text );
                }

                connectionRequest.Comments = sb.ToString();

                connectionRequestService.Add( connectionRequest );
                rockContext.SaveChanges();
            }
        }

        /// <summary>
        /// Creates the prayer request.
        /// </summary>
        /// <param name="rockContext">The rock context.</param>
        /// <param name="person">The person.</param>
        private void CreatePrayerRequest( RockContext rockContext, Person person )
        {
            if ( person != null && !tbPrayerRequests.Text.IsNullOrWhiteSpace() )
            {
                PrayerRequest prayerRequest = new PrayerRequest();
                prayerRequest.RequestedByPersonAliasId = person.PrimaryAliasId;
                prayerRequest.FirstName = person.NickName;
                prayerRequest.LastName = person.LastName;
                prayerRequest.Text = tbPrayerRequests.Text;
                prayerRequest.Email = person.Email;

                Category category;
                Guid defaultCategoryGuid = GetAttributeValue( "PrayerCategory" ).AsGuid();
                if ( !defaultCategoryGuid.IsEmpty() )
                {
                    category = new CategoryService( rockContext ).Get( defaultCategoryGuid );
                    prayerRequest.CategoryId = category.Id;
                    prayerRequest.Category = category;
                }

                prayerRequest.IsPublic = false;

                PrayerRequestService prayerRequestService = new PrayerRequestService( rockContext );
                prayerRequestService.Add( prayerRequest );
                prayerRequest.EnteredDateTime = RockDateTime.Now;
                rockContext.SaveChanges();
            }
        }

        /// <summary>
        /// Adds a new child.
        /// </summary>
        private void AddChild()
        {
            var person = new Person();
            person.Guid = Guid.NewGuid();
            person.Gender = Gender.Unknown;
            person.LastName = tbLastName.Text;
            person.GradeOffset = null;

            _dvcChildConnectionStatus = DefinedValueCache.Get( GetAttributeValue( "ChildConnectionStatus" ).AsGuid() );
            if ( _dvcChildConnectionStatus != null )
            {
                person.ConnectionStatusValueId = _dvcChildConnectionStatus.Id;
            }
            else
            {
                _dvcChildConnectionStatus = DefinedValueCache.Get( Rock.SystemGuid.DefinedValue.PERSON_CONNECTION_STATUS_VISITOR.AsGuid() );
            }

            var child = new PreRegistrationChild( person );

            Children.Add( child );
        }

        /// <summary>
        /// Gets the children data.
        /// </summary>
        private void GetChildrenData()
        {
            Children = new List<PreRegistrationChild>();

            foreach ( var childRow in prChildren.ChildRows )
            {
                var person = new Person();
                person.Id = childRow.PersonId;
                person.Guid = childRow.PersonGuid ?? Guid.NewGuid();
                person.NickName = childRow.NickName;
                person.LastName = childRow.LastName;
                person.SuffixValueId = childRow.SuffixValueId;
                person.Gender = childRow.Gender;
                person.SetBirthDate( childRow.BirthDate );
                person.GradeOffset = childRow.GradeOffset;

                var child = new PreRegistrationChild( person );

                child.MobilePhoneNumber = childRow.MobilePhone;
                child.MobileCountryCode = childRow.MobilePhoneCountryCode;

                child.RelationshipType = childRow.RelationshipType;

                Children.Add( child );
            }
        }

        #endregion
    }
}