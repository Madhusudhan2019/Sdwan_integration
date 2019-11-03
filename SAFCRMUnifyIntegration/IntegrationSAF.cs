using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.ServiceModel;
using System.Text;
using System.Xml;

namespace SAFCRMUnifyIntegration
{
    public class IntegrationSAF : IPlugin
    {
        ITracingService tracingService;
        string areaname = string.Empty;
        string Buildingname = string.Empty;
        Guid Productsegmentid = new Guid();
        public string ShortnameUnify = string.Empty;
        string PRDSeg = null;
        int industryname;
      
        public string NewAccountid;
        private string configData = string.Empty;
        String childshortname = String.Empty;
        String billingContactfirst = String.Empty;
        Guid CreateAccountId = new Guid();
        // Guid NewAccountid = new Guid();
        private Dictionary<string, string> globalConfig = new Dictionary<string, string>();
        public IntegrationSAF(string unsecureString, string secureString)
        {

            if (String.IsNullOrWhiteSpace(unsecureString) || String.IsNullOrWhiteSpace(secureString))
            {
                this.configData = unsecureString;
                this.ReadUnSecuredConfig(this.configData);
            }
            else
            {
                this.configData = unsecureString;
                this.ReadUnSecuredConfig(this.configData);
            }
        }
        public void Execute(IServiceProvider serviceProvider)
        {
            String SafNo = String.Empty;
            String CanNo = String.Empty;
            string CafInsCityRevGrpId = String.Empty;


            String Tan = String.Empty;
            String Pan = String.Empty;
            String Gst = String.Empty;

            String childName = String.Empty;
            String childFullName = String.Empty;
            string receiptLedgerAccountNo = string.Empty;
            string coaGroupNo = string.Empty;
            string coaNo = string.Empty;
            string ledgerBookNo = string.Empty;
            string ledgerParentId = string.Empty;
            string placeOfConsumptionId = string.Empty;
            String childDomainId = String.Empty;


            String LedgerparentId = String.Empty;
            String requestXml = String.Empty;
            string localIP = "?";


            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);
            tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            try
            {
                if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity)
                {

                    Entity SAFID = (Entity)context.InputParameters["Target"];
                    if (SAFID.LogicalName != "onl_saf")
                        return;

                    Entity SAF = context.PostEntityImages["PostImage"];

                    tracingService.Trace("Entity id " + SAF.Id);


                    //if (SAF.Attributes.Contains("onl_spectra_accountid"))
                    //{
                    if (SAF.GetAttributeValue<OptionSetValue>("onl_status").Value == 122050002)
                    {
                        Entity oppRef1 = service.Retrieve("onl_saf", SAF.Id, new ColumnSet("onl_opportunityidid"));
                        EntityReference ownerLookup = (EntityReference)oppRef1.Attributes["onl_opportunityidid"];

                        var opportunityName = ownerLookup.Name;
                        Guid opportunityid = ownerLookup.Id; // getting opportunity id based on SAFID

                        //getting Area id on behalf of opportunity id mapping
                        Entity Parent_area = service.Retrieve("opportunity", opportunityid, new ColumnSet("alletech_area"));
                        //lookup area Entity   
                        EntityReference AreaID = (EntityReference)Parent_area.Attributes["alletech_area"];
                        Guid Areaid = AreaID.Id; // getting Area id based on Opportunity id


                        Entity Parent_Buildingname = service.Retrieve("alletech_area", Areaid, new ColumnSet("alletech_name"));
                        //lookup Building Name
                        var Areaname = Parent_Buildingname.GetAttributeValue<String>("alletech_name");
                        areaname = Areaname;

                        //Alletech Buliding Name
                        Entity Parent_buliding = service.Retrieve("opportunity", opportunityid, new ColumnSet("alletech_buildingname"));
                        //lookup Building Name
                        EntityReference BuldingID = (EntityReference)Parent_buliding.Attributes["alletech_buildingname"];
                        Guid bID = BuldingID.Id;//getting Building Name id based on Opportunity id

                        Buildingname = BuldingID.Name;

                        int verticalsegmentno;
                        Entity oppty = service.Retrieve("opportunity", opportunityid, new ColumnSet("spectra_customersegmentcode"));
                        if (oppty.Attributes.Contains("spectra_customersegmentcode"))
                        {
                            string customersegment = oppty.FormattedValues["spectra_customersegmentcode"].ToString();

                            if (customersegment.ToLower() == "smb")
                                industryname = 22;//smb
                            else if (customersegment.ToLower() == "la")
                                industryname = 23;//LA
                            else if (customersegment.ToLower() == "media")
                                industryname = 10;//media
                            else if (customersegment.ToLower() == "sp")
                                industryname = 11;//service provider
                        }
                        else
                        {
                            industryname = 22;
                            tracingService.Trace("Customer Segment is Empty on Lead");
                        }
                        //Product Segment

                        Entity Productrecord = service.Retrieve("onl_saf", SAF.Id, new ColumnSet("onl_spectra_productsegment"));

                        if (Productrecord.Attributes.Contains("onl_spectra_productsegment"))
                        {
                            string Productsegment = ((EntityReference)(Productrecord.Attributes["onl_spectra_productsegment"])).Name;
                            Productsegmentid = ((EntityReference)(Productrecord.Attributes["onl_spectra_productsegment"])).Id;

                            
                                verticalsegmentno = 8;
                           
                        }
                        else
                        {
                            tracingService.Trace("Product Segment is not configured on Product");
                        }
                        if (SAF.Attributes.Contains("onl_spectra_accountid"))
                        {
                            CanNo = SAF.GetAttributeValue<String>("onl_spectra_accountid");
                        }
                        else
                        {

                            Entity Parent_accountid = service.Retrieve("opportunity", opportunityid, new ColumnSet("alletech_accountid"));
                            CanNo = Parent_accountid.GetAttributeValue<String>("alletech_accountid");
                        }

                        #region Add Child account updation
                        //call child account update 
                        ChildAccountUpdation(service, SAF, opportunityid, context);
                        #endregion

                        #region Check Parent account condition 
                        if (!SAF.Attributes.Contains("onl_parentaccountonl"))
                        {
                            ParentAccountCreation(service, ref SAF, opportunityid, context, CanNo);
                            AccountShortName1(service, SAF, context, CanNo);
                        }

                        #endregion
                        Entity SAF1 = service.Retrieve("onl_saf", SAF.Id, new ColumnSet(true));

                        ConditionExpression condition = new ConditionExpression("alletech_accountid", ConditionOperator.Equal, CanNo);
                        FilterExpression filter = new FilterExpression();
                        filter.AddCondition(condition);
                        filter.FilterOperator = LogicalOperator.And;
                        QueryExpression query = new QueryExpression
                        {
                            EntityName = "account",
                            ColumnSet = new ColumnSet(true),
                            Criteria = filter,
                        };
                        EntityCollection childAccountCollection = service.RetrieveMultiple(query);
                        Entity childAccount = childAccountCollection.Entities[0];

                        if (childAccount.Attributes.Contains("name"))
                            childName = childAccount.GetAttributeValue<String>("name");


                        if (childAccount.Attributes.Contains("alletech_unifyshortname"))
                            childshortname = childAccount.GetAttributeValue<String>("alletech_unifyshortname");

                        string paccshort = childshortname.Split('-')[0];


                        #region billing record start

                        SafNo = SAF.GetAttributeValue<String>("onl_name");//getting SAF Name
                        Tan = SAF.GetAttributeValue<String>("onl_tanno");//getting TAN Id on behalf of safid
                        Pan = SAF.GetAttributeValue<String>("onl_panno");//getting PAN Id on behalf of safid
                        Gst = SAF.GetAttributeValue<String>("onl_gstnumber");//getting PAN Id on behalf of safid




                        Entity Parent_city = service.Retrieve("opportunity", opportunityid, new ColumnSet("alletech_city"));
                        //lookup area Entity   
                        EntityReference ECityId = (EntityReference)Parent_city.Attributes["alletech_city"];
                        //get City Id
                        Guid pCityId = ECityId.Id;
                        var cityname = ECityId.Name;


                        Entity oppsocalmedia = service.Retrieve("opportunity", opportunityid, new ColumnSet("alletech_facebookid", "alletech_twitterid", "alletech_emailid", "alletech_mobileno", "alletech_salutation"));
                        var Facebook = oppsocalmedia.GetAttributeValue<String>("alletech_facebookid");
                        var Twitter = oppsocalmedia.GetAttributeValue<String>("alletech_twitterid");
                        var Email = oppsocalmedia.GetAttributeValue<String>("alletech_emailid");
                        var Mobile = oppsocalmedia.GetAttributeValue<String>("alletech_mobileno");
                        var salutation = oppsocalmedia.GetAttributeValue<OptionSetValue>("alletech_salutation").Value.ToString();




                        String firstnameAccShip = String.Empty;
                        String lastnameAccShip = String.Empty;
                        String billCityId = String.Empty;
                        String ShipCityId = String.Empty;
                        String firstName = String.Empty;
                        String lastName = String.Empty;

                        String shipsitecity = String.Empty;
                        String billToStreet = String.Empty;
                        String shipToStreet = String.Empty;
                        String billToPincode = String.Empty;
                        String shipToPincode = String.Empty;
                        String billToState = String.Empty;
                        String shipToState = String.Empty;
                        String billToplotno = String.Empty;
                        String billTofloor = String.Empty;
                        String billTobuildingname = String.Empty;
                        String billTolandmark = String.Empty;
                        String billtostreetcomplete = String.Empty;
                        String billtoarea = String.Empty;
                        String shipToplotno = String.Empty;
                        String shipTofloor = String.Empty;
                        String shipTobuildingname = String.Empty;
                        String shipTolandmark = String.Empty;
                        String shiptostreetcomplete = String.Empty;
                        String shiptoarea = String.Empty;
                        String lastnameBill = String.Empty;
                        string shipToblock = string.Empty;

                        // Payment
                        string paymentAmount = string.Empty;
                        decimal payamt = 0;
                        string securityAmount = string.Empty;
                        string paymentType = string.Empty;
                        string paymentDate = string.Empty;
                        DateTime paymentDateString = new DateTime();
                        string chequeNo = string.Empty;
                        int bankNo = 0;
                        string banBranch = string.Empty;
                        string securityDepositType = string.Empty;


                        string creditcardno = string.Empty;
                        string approvalcodecredit = string.Empty;
                        string debitcardno = string.Empty;
                        string approvalcodedebit = string.Empty;


                        tracingService.Trace("In Bill TO and Ship to Variables");
                        if (SAF.Attributes.Contains("onl_cityonl"))
                        {
                            Entity city = service.Retrieve("alletech_city", SAF.GetAttributeValue<EntityReference>("onl_cityonl").Id, new ColumnSet("alletech_cityno"));
                            billCityId = city.GetAttributeValue<String>("alletech_cityno");
                            billCityId = string.IsNullOrWhiteSpace(billCityId) ? "0" : billCityId;
                        }
                        if (SAF.Attributes.Contains("onl_buildingnoplotnoonl"))
                        {
                            billToplotno = SAF.GetAttributeValue<string>("onl_buildingnoplotnoonl");
                            billToplotno = billToplotno + ", ";
                        }
                        if (SAF.Attributes.Contains("onl_floor"))
                        {

                            billTofloor = SAF.GetAttributeValue<string>("onl_floor");
                            billTofloor = billTofloor + ", ";
                            billTofloor = "Floor - " + billTofloor;
                        }
                        if (SAF.Attributes.Contains("onl_buildingnameonl"))
                        {

                            if (SAF.GetAttributeValue<EntityReference>("onl_buildingnameonl").Name.ToLower() == "other" && SAF.Attributes.Contains("onl_specifybuildingonl"))
                            {
                                billTobuildingname = SAF.GetAttributeValue<string>("onl_specifybuildingonl");
                                billTobuildingname = billTobuildingname + ", ";
                            }
                            else if (SAF.GetAttributeValue<EntityReference>("onl_buildingnameonl").Name.ToLower() != "other")
                            {
                                billTobuildingname = SAF.GetAttributeValue<EntityReference>("onl_buildingnameonl").Name;
                                billTobuildingname = billTobuildingname + ", ";
                            }
                        }
                        if (SAF.Attributes.Contains("onl_areaonl"))
                        {
                            if (SAF.GetAttributeValue<EntityReference>("onl_areaonl").Name.ToLower() == "other" && SAF.Attributes.Contains("onl_specifyareaonl"))
                            {
                                billtoarea = SAF.GetAttributeValue<string>("onl_specifyareaonl");
                                billtoarea = billtoarea + ", ";
                            }
                            else if (SAF.GetAttributeValue<EntityReference>("onl_areaonl").Name.ToLower() != "other")
                            {
                                billtoarea = SAF.GetAttributeValue<EntityReference>("onl_areaonl").Name;
                                billtoarea = billtoarea + ", ";
                            }
                        }
                        if (SAF.Attributes.Contains("onl_landmarkifany"))
                        {
                            billTolandmark = SAF.GetAttributeValue<string>("onl_landmarkifany");
                        }

                        if (SAF.Attributes.Contains("onl_street"))
                        {
                            billToStreet = SAF.GetAttributeValue<String>("onl_street");
                            billToStreet = billToStreet + ", ";
                        }

                        billtostreetcomplete = billToplotno + billTofloor + billTobuildingname + billToStreet + billtoarea + billTolandmark;


                        //Entity childbillpincode = service.Retrieve("onl_saf", SAF.Id, new ColumnSet("onl_pincode"));
                        if (SAF.Attributes.Contains("onl_pincode"))
                        {
                            billToPincode = SAF.GetAttributeValue<string>("onl_pincode").Replace(" ", "");
                        }

                        //Entity childbillstate = service.Retrieve("onl_saf", SAF.Id, new ColumnSet("onl_stateonl"));
                        if (SAF.Attributes.Contains("onl_stateonl"))
                        {
                            Entity state = service.Retrieve("alletech_state", SAF.GetAttributeValue<EntityReference>("onl_stateonl").Id, new ColumnSet("alletech_statename"));
                            billToState = state.GetAttributeValue<String>("alletech_statename");
                        }
                        //Entity childsepectrapincode = service.Retrieve("onl_saf", SAF.Id, new ColumnSet("onl_spectra_pincode"));
                        //if (SAF.Attributes.Contains("onl_spectra_pincode"))
                        //    shipToPincode = SAF.GetAttributeValue<String>("onl_spectra_pincode").Replace(" ", "");


                        //Entity childsepectrastate = service.Retrieve("onl_saf", SAF.Id, new ColumnSet("onl_spectra_state"));
                        //if (SAF.Attributes.Contains("onl_spectra_state"))
                        //{
                        //    Entity state = service.Retrieve("alletech_state", SAF.GetAttributeValue<EntityReference>("onl_spectra_state").Id, new ColumnSet("alletech_statename"));
                        //    shipToState = state.GetAttributeValue<String>("alletech_statename");
                        //}
                        #endregion

                        #region Parent Account record 

                        tracingService.Trace("After Billing street");

                        #region Concatenated Street Shipping
                        if (SAF.Attributes.Contains("onl_spectra_buildingnoplotno"))
                        {
                            shipToplotno = SAF.GetAttributeValue<string>("onl_spectra_buildingnoplotno");
                            shipToplotno = shipToplotno + ", ";
                        }
                        if (SAF.Attributes.Contains("onl_spectra_floor"))
                        {
                            shipTofloor = SAF.GetAttributeValue<string>("onl_spectra_floor");
                            shipTofloor = "Floor - " + shipTofloor + ", ";
                        }
                        if (SAF.Attributes.Contains("onl_spectra_buildingname"))
                        {
                            if (SAF.GetAttributeValue<EntityReference>("onl_spectra_buildingname").Name.ToLower() == "other" && SAF.Attributes.Contains("onl_spectra_specifybuilding"))
                            {
                                shipTobuildingname = SAF.GetAttributeValue<string>("onl_spectra_specifybuilding");
                                shipTobuildingname = shipTobuildingname + ", ";
                            }
                            else if (SAF.GetAttributeValue<EntityReference>("onl_spectra_buildingname").Name.ToLower() != "other")
                            {
                                shipTobuildingname = SAF.GetAttributeValue<EntityReference>("onl_spectra_buildingname").Name;
                                shipTobuildingname = shipTobuildingname + ", ";
                            }
                        }
                        if (SAF.Attributes.Contains("onl_spectra_area"))
                        {
                            if (SAF.GetAttributeValue<EntityReference>("onl_spectra_area").Name.ToLower() == "other" && SAF.Attributes.Contains("onl_spectra_specifyarea"))
                            {
                                shiptoarea = SAF.GetAttributeValue<string>("onl_spectra_specifyarea");
                                shiptoarea = shiptoarea + ", ";
                            }
                            else if (SAF.GetAttributeValue<EntityReference>("onl_spectra_area").Name.ToLower() != "other")
                            {
                                shiptoarea = SAF.GetAttributeValue<EntityReference>("onl_spectra_area").Name;
                                shiptoarea = shiptoarea + ", ";
                            }
                        }
                        if (SAF.Attributes.Contains("onl_spectra_landmark"))
                        {
                            shipTolandmark = SAF.GetAttributeValue<string>("onl_spectra_landmark");
                        }
                        if (SAF.Attributes.Contains("onl_spectra_block"))
                        {
                            shipToblock = SAF.GetAttributeValue<string>("onl_spectra_block");
                            shipToblock = "block- " + shipToblock + ", ";
                        }

                        if (SAF.Attributes.Contains("onl_spectra_street"))
                        {
                            shipToStreet = SAF.GetAttributeValue<String>("onl_spectra_street");
                            shipToStreet = shipToStreet + ", ";
                        }

                        shiptostreetcomplete = shipToplotno + shipTofloor + shipToblock + shipTobuildingname + shipToStreet + shiptoarea + shipTolandmark;
                        #endregion

                        //Entity childspectracity = service.Retrieve("onl_saf", SAF.Id, new ColumnSet("onl_spectra_city"));
                        if (SAF.Attributes.Contains("onl_spectra_city"))
                        {
                            Entity city = service.Retrieve("alletech_city", SAF.GetAttributeValue<EntityReference>("onl_spectra_city").Id, new ColumnSet("alletech_cityno"));
                            ShipCityId = city.GetAttributeValue<String>("alletech_cityno");
                        }

                        #endregion


                        //payment fields start

                        #region payment mode type and bank details

                        Entity cafPayment1 = service.Retrieve("onl_saf", SAF.Id, new ColumnSet("onl_payinslip"));


                        EntityReference cafPayment = cafPayment1.GetAttributeValue<EntityReference>("onl_payinslip");


                        Entity payment = service.Retrieve("alletech_payment", cafPayment.Id, new ColumnSet(true));


                        if (payment.Attributes.Contains("alletech_paymentmodetype"))
                        {
                            // Payment Mode== CASH
                            if (payment.GetAttributeValue<OptionSetValue>("alletech_paymentmodetype").Equals(new OptionSetValue(10)))
                            {

                                paymentType = "unify.finance.instrument.type.cash";

                                if (payment.Attributes.Contains("alletech_amount"))
                                    payamt = payment.GetAttributeValue<Money>("alletech_amount").Value;

                                if (payment.Attributes.Contains("alletech_securitydepositifapplicable"))
                                    securityAmount = payment.GetAttributeValue<string>("alletech_securitydepositifapplicable");

                                if (payment.Attributes.Contains("alletech_securitydeposittype"))
                                    securityDepositType = payment.GetAttributeValue<OptionSetValue>("alletech_securitydeposittype").Value.ToString();

                                if (payment.Attributes.Contains("alletech_paymentdate"))
                                    paymentDateString = TimeZoneInfo.ConvertTimeFromUtc(Convert.ToDateTime(payment.Attributes["alletech_paymentdate"]), TimeZoneInfo.Local);//payment.GetAttributeValue<DateTime>("alletech_paymentdate").AddHours(5).AddMinutes(30).ToShortDateString();
                                paymentDate = DateFormater(paymentDateString);
                            }
                            //Payment Mode== CHEQUE
                            if (payment.GetAttributeValue<OptionSetValue>("alletech_paymentmodetype").Equals(new OptionSetValue(1)))
                            {
                                paymentType = "unify.finance.instrument.type.cheque";
                                if (payment.Attributes.Contains("alletech_amount"))
                                    payamt = payment.GetAttributeValue<Money>("alletech_amount").Value;
                                if (payment.Attributes.Contains("alletech_securitydepositifapplicable"))
                                    securityAmount = payment.GetAttributeValue<string>("alletech_securitydepositifapplicable");

                                if (payment.Attributes.Contains("alletech_securitydeposittype"))
                                    securityDepositType = payment.GetAttributeValue<OptionSetValue>("alletech_securitydeposittype").Value.ToString();

                                if (payment.Attributes.Contains("alletech_chequeddno"))
                                    chequeNo = payment.GetAttributeValue<string>("alletech_chequeddno");
                                if (payment.Attributes.Contains("alletech_chequeddissuedate"))
                                    paymentDateString = TimeZoneInfo.ConvertTimeFromUtc(Convert.ToDateTime(payment.Attributes["alletech_chequeddissuedate"]), TimeZoneInfo.Local);//payment.GetAttributeValue<DateTime>("alletech_paymentdate").AddHours(5).AddMinutes(30).ToShortDateString();
                                paymentDate = DateFormater(paymentDateString);

                                EntityReference bankname = payment.GetAttributeValue<EntityReference>("alletech_banknameid");
                                Entity _bank = service.Retrieve("alletech_bank", bankname.Id, new ColumnSet("alletech_banknameid"));
                                if (_bank.Attributes.Contains("alletech_banknameid"))
                                {
                                    bankNo = _bank.GetAttributeValue<Int32>("alletech_banknameid");
                                }
                                if (payment.Attributes.Contains("alletech_branch"))
                                    banBranch = payment.GetAttributeValue<string>("alletech_branch");

                            }
                            //Payment Mode== CREDIT CARD
                            if (payment.GetAttributeValue<OptionSetValue>("alletech_paymentmodetype").Equals(new OptionSetValue(7)))
                            {
                                paymentType = "unify.finance.instrument.type.creditCard";
                                if (payment.Attributes.Contains("alletech_amount"))
                                    payamt = payment.GetAttributeValue<Money>("alletech_amount").Value;
                                if (payment.Attributes.Contains("alletech_securitydepositifapplicable"))
                                    securityAmount = payment.GetAttributeValue<string>("alletech_securitydepositifapplicable");

                                if (payment.Attributes.Contains("alletech_securitydeposittype"))
                                    securityDepositType = payment.GetAttributeValue<OptionSetValue>("alletech_securitydeposittype").Value.ToString();

                                if (payment.Attributes.Contains("alletech_refrenceid"))
                                    approvalcodecredit = payment.GetAttributeValue<string>("alletech_refrenceid");
                                if (payment.Attributes.Contains("alletech_creditcardlast4digitno"))
                                    creditcardno = payment.GetAttributeValue<string>("alletech_creditcardlast4digitno");

                                chequeNo = approvalcodecredit + " - " + creditcardno;

                                //if (payment.Attributes.Contains("alletech_paymentdate"))
                                if (payment.Attributes.Contains("alletech_paymentdate"))
                                    paymentDateString = TimeZoneInfo.ConvertTimeFromUtc(Convert.ToDateTime(payment.Attributes["alletech_paymentdate"]), TimeZoneInfo.Local);//payment.GetAttributeValue<DateTime>("alletech_paymentdate").AddHours(5).AddMinutes(30).ToShortDateString();
                                paymentDate = DateFormater(paymentDateString);
                            }


                            //Payment Mode== DEBIT CARD
                            if (payment.GetAttributeValue<OptionSetValue>("alletech_paymentmodetype").Equals(new OptionSetValue(8)))
                            {
                                paymentType = "unify.finance.instrument.type.debitCard";
                                if (payment.Attributes.Contains("alletech_amount"))
                                    payamt = payment.GetAttributeValue<Money>("alletech_amount").Value;
                                if (payment.Attributes.Contains("alletech_securitydepositifapplicable"))
                                    securityAmount = payment.GetAttributeValue<string>("alletech_securitydepositifapplicable");

                                if (payment.Attributes.Contains("alletech_securitydeposittype"))
                                    securityDepositType = payment.GetAttributeValue<OptionSetValue>("alletech_securitydeposittype").Value.ToString();

                                if (payment.Attributes.Contains("alletech_debitcardapprovalcode"))
                                    approvalcodedebit = payment.GetAttributeValue<string>("alletech_debitcardapprovalcode");
                                if (payment.Attributes.Contains("alletech_debitcardlast4digitno"))
                                    debitcardno = payment.GetAttributeValue<string>("alletech_debitcardlast4digitno");

                                chequeNo = approvalcodedebit + " - " + debitcardno;

                                //if (payment.Attributes.Contains("alletech_paymentdate"))
                                if (payment.Attributes.Contains("alletech_paymentdate"))
                                    paymentDateString = TimeZoneInfo.ConvertTimeFromUtc(Convert.ToDateTime(payment.Attributes["alletech_paymentdate"]), TimeZoneInfo.Local);//payment.GetAttributeValue<DateTime>("alletech_paymentdate").AddHours(5).AddMinutes(30).ToShortDateString();
                                paymentDate = DateFormater(paymentDateString);
                            }

                            //Payment Mode== DD
                            if (payment.GetAttributeValue<OptionSetValue>("alletech_paymentmodetype").Equals(new OptionSetValue(2)))
                            {
                                paymentType = "unify.finance.instrument.type.demanddraft";
                                if (payment.Attributes.Contains("alletech_amount"))
                                    payamt = payment.GetAttributeValue<Money>("alletech_amount").Value;
                                if (payment.Attributes.Contains("alletech_securitydepositifapplicable"))
                                    securityAmount = payment.GetAttributeValue<string>("alletech_securitydepositifapplicable");

                                if (payment.Attributes.Contains("alletech_securitydeposittype"))
                                    securityDepositType = payment.GetAttributeValue<OptionSetValue>("alletech_securitydeposittype").Value.ToString();

                                if (payment.Attributes.Contains("alletech_chequeddno"))
                                    chequeNo = payment.GetAttributeValue<string>("alletech_chequeddno");
                                if (payment.Attributes.Contains("alletech_chequeddissuedate"))
                                    paymentDateString = TimeZoneInfo.ConvertTimeFromUtc(Convert.ToDateTime(payment.Attributes["alletech_chequeddissuedate"]), TimeZoneInfo.Local);//payment.GetAttributeValue<DateTime>("alletech_paymentdate").AddHours(5).AddMinutes(30).ToShortDateString();
                                paymentDate = DateFormater(paymentDateString);

                                EntityReference bankname = payment.GetAttributeValue<EntityReference>("alletech_banknameid");
                                Entity _bank = service.Retrieve("alletech_bank", bankname.Id, new ColumnSet("alletech_banknameid"));
                                if (_bank.Attributes.Contains("alletech_banknameid"))
                                {
                                    bankNo = _bank.GetAttributeValue<Int32>("alletech_banknameid");

                                }
                                if (payment.Attributes.Contains("alletech_branch"))
                                    banBranch = payment.GetAttributeValue<string>("alletech_branch");

                            }

                            //Payment Mode== RTGS
                            if (payment.GetAttributeValue<OptionSetValue>("alletech_paymentmodetype").Equals(new OptionSetValue(5)))
                            {
                                paymentType = "unify.finance.instrument.type.rtgs";
                                if (payment.Attributes.Contains("alletech_amount"))
                                    payamt = payment.GetAttributeValue<Money>("alletech_amount").Value;
                                if (payment.Attributes.Contains("alletech_transactionreferenceid"))
                                    chequeNo = payment.GetAttributeValue<string>("alletech_transactionreferenceid");
                                if (payment.Attributes.Contains("alletech_securitydepositifapplicable"))
                                    securityAmount = payment.GetAttributeValue<string>("alletech_securitydepositifapplicable");

                                if (payment.Attributes.Contains("alletech_securitydeposittype"))
                                    securityDepositType = payment.GetAttributeValue<OptionSetValue>("alletech_securitydeposittype").Value.ToString();

                                //if (payment.Attributes.Contains("alletech_paymentdate"))
                                if (payment.Attributes.Contains("alletech_paymentdate"))
                                    paymentDateString = TimeZoneInfo.ConvertTimeFromUtc(Convert.ToDateTime(payment.Attributes["alletech_paymentdate"]), TimeZoneInfo.Local);//payment.GetAttributeValue<DateTime>("alletech_paymentdate").AddHours(5).AddMinutes(30).ToShortDateString();
                                paymentDate = DateFormater(paymentDateString);
                            }

                            //Payment Mode== NEFT
                            if (payment.GetAttributeValue<OptionSetValue>("alletech_paymentmodetype").Equals(new OptionSetValue(4)))
                            {
                                paymentType = "unify.finance.instrument.type.neft";
                                if (payment.Attributes.Contains("alletech_amount"))
                                    payamt = payment.GetAttributeValue<Money>("alletech_amount").Value;
                                if (payment.Attributes.Contains("alletech_transactionreferenceid"))
                                    chequeNo = payment.GetAttributeValue<string>("alletech_transactionreferenceid");
                                if (payment.Attributes.Contains("alletech_securitydepositifapplicable"))
                                    securityAmount = payment.GetAttributeValue<string>("alletech_securitydepositifapplicable");

                                if (payment.Attributes.Contains("alletech_securitydeposittype"))
                                    securityDepositType = payment.GetAttributeValue<OptionSetValue>("alletech_securitydeposittype").Value.ToString();

                                // if (payment.Attributes.Contains("alletech_paymentdate"))
                                if (payment.Attributes.Contains("alletech_paymentdate"))
                                    paymentDateString = TimeZoneInfo.ConvertTimeFromUtc(Convert.ToDateTime(payment.Attributes["alletech_paymentdate"]), TimeZoneInfo.Local);//payment.GetAttributeValue<DateTime>("alletech_paymentdate").AddHours(5).AddMinutes(30).ToShortDateString();
                                paymentDate = DateFormater(paymentDateString);
                            }

                            //Paymentmode == EZETAP
                            if (payment.GetAttributeValue<OptionSetValue>("alletech_paymentmodetype").Equals(new OptionSetValue(11)))
                            {
                                paymentType = "unify.finance.instrument.type.ezetap";
                                if (payment.Attributes.Contains("alletech_amount"))
                                    payamt = payment.GetAttributeValue<Money>("alletech_amount").Value;
                                if (payment.Attributes.Contains("alletech_transactionreferenceid"))
                                    chequeNo = payment.GetAttributeValue<string>("alletech_transactionreferenceid");
                                if (payment.Attributes.Contains("alletech_securitydepositifapplicable"))
                                    securityAmount = payment.GetAttributeValue<string>("alletech_securitydepositifapplicable");

                                if (payment.Attributes.Contains("alletech_securitydeposittype"))
                                    securityDepositType = payment.GetAttributeValue<OptionSetValue>("alletech_securitydeposittype").Value.ToString();

                                // if (payment.Attributes.Contains("alletech_paymentdate"))
                                if (payment.Attributes.Contains("alletech_paymentdate"))
                                    paymentDateString = TimeZoneInfo.ConvertTimeFromUtc(Convert.ToDateTime(payment.Attributes["alletech_paymentdate"]), TimeZoneInfo.Local);//payment.GetAttributeValue<DateTime>("alletech_paymentdate").AddHours(5).AddMinutes(30).ToShortDateString();
                                paymentDate = DateFormater(paymentDateString);
                            }

                            //Paymentmode == EZETAP - CHEQUE
                            if (payment.GetAttributeValue<OptionSetValue>("alletech_paymentmodetype").Equals(new OptionSetValue(12)))
                            {
                                paymentType = "unify.finance.instrument.type.ezetapcheque";
                                if (payment.Attributes.Contains("alletech_amount"))
                                    payamt = payment.GetAttributeValue<Money>("alletech_amount").Value;
                                if (payment.Attributes.Contains("alletech_securitydepositifapplicable"))
                                    securityAmount = payment.GetAttributeValue<string>("alletech_securitydepositifapplicable");

                                if (payment.Attributes.Contains("alletech_securitydeposittype"))
                                    securityDepositType = payment.GetAttributeValue<OptionSetValue>("alletech_securitydeposittype").Value.ToString();

                                if (payment.Attributes.Contains("alletech_chequeddno"))
                                    chequeNo = payment.GetAttributeValue<string>("alletech_chequeddno");
                                if (payment.Attributes.Contains("alletech_chequeddissuedate"))
                                    paymentDateString = TimeZoneInfo.ConvertTimeFromUtc(Convert.ToDateTime(payment.Attributes["alletech_chequeddissuedate"]), TimeZoneInfo.Local);//payment.GetAttributeValue<DateTime>("alletech_paymentdate").AddHours(5).AddMinutes(30).ToShortDateString();
                                paymentDate = DateFormater(paymentDateString);

                                EntityReference bankname = payment.GetAttributeValue<EntityReference>("alletech_banknameid");
                                Entity _bank = service.Retrieve("alletech_bank", bankname.Id, new ColumnSet("alletech_banknameid"));
                                if (_bank.Attributes.Contains("alletech_banknameid"))
                                    bankNo = _bank.GetAttributeValue<Int32>("alletech_banknameid");

                                if (payment.Attributes.Contains("alletech_branch"))
                                    banBranch = payment.GetAttributeValue<string>("alletech_branch");
                            }
                        }

                        #endregion

                        //payment fields end

                        if (SAF.Attributes.Contains("onl_contactpersonname2"))
                            billingContactfirst = SAF.GetAttributeValue<String>("onl_contactpersonname2");

                        string[] ssize = billingContactfirst.Split(null);
                        if (ssize.Length > 1)
                        {
                            firstnameAccShip = ssize[0];
                            for (int k = 1; k < ssize.Length; k++)
                            {
                                lastName = lastName + " " + ssize[k];
                            }
                        }
                        else
                            firstnameAccShip = ssize[0];


                        #region IP Address of Machine

                        //IPHostEntry host;

                        //host = Dns.GetHostEntry(Dns.GetHostName());
                        //foreach (IPAddress ip in host.AddressList)
                        //{
                        //    if (ip.AddressFamily == AddressFamily.InterNetwork)
                        //    {
                        //        localIP = ip.ToString();
                        //        break;
                        //    }
                        //}
                        #endregion
                        //if (!SAF.Attributes.Contains("onl_parentaccountonl"))
                        //{
                        #region Without Parent Account
                        bool flag = false;
                        // Check total no of Opportunity Sites  
                        QueryExpression querycustomersite = new QueryExpression();
                        querycustomersite.EntityName = "onl_customersite";
                        querycustomersite.ColumnSet = new ColumnSet(true);
                        querycustomersite.Criteria.AddCondition("onl_opportunityidid", ConditionOperator.Equal, opportunityid);
                        EntityCollection resultcustomersite = service.RetrieveMultiple(querycustomersite);
                        decimal Sitecount = resultcustomersite.Entities.Count;
                        //Payment Amount Divided by Total no of Sites
                        paymentAmount = (payamt / Sitecount).ToString();
                        //Security Amount Divided by Total  no of Sites
                        if (securityAmount != null && securityAmount != string.Empty)
                            securityAmount = (decimal.Parse(securityAmount) / Sitecount).ToString();

                        int rowcount = 1;
                        foreach (Entity entity in resultcustomersite.Entities)
                        {
                            if (rowcount > 1)
                            {
                                //

                                string CreateSiteAccount = "CreateSiteAccount";
                                //Generate Account Based on Site Id & Get Account Created Id
                                CanNo = ChildcountSitesCreation(service, ref SAF, opportunityid, context, rowcount, childshortname, CreateSiteAccount, entity);

                            }
                            #region getting site details

                            //city id based on each site
                            Guid sitecity = entity.GetAttributeValue<EntityReference>("onl_city").Id;
                            string sitecityname = entity.GetAttributeValue<EntityReference>("onl_city").Name;
                            if (entity.Attributes.Contains("onl_city"))
                            {
                                Entity Sitecity = service.Retrieve("alletech_city", entity.GetAttributeValue<EntityReference>("onl_city").Id, new ColumnSet("alletech_cityno"));
                                shipsitecity = Sitecity.GetAttributeValue<String>("alletech_cityno");
                                shipsitecity = string.IsNullOrWhiteSpace(shipsitecity) ? "0" : shipsitecity;
                            }
                            if (entity.Attributes.Contains("onl_state"))
                            {
                                Entity state = service.Retrieve("alletech_state", entity.GetAttributeValue<EntityReference>("onl_state").Id, new ColumnSet("alletech_statename"));
                                shipToState = state.GetAttributeValue<String>("alletech_statename");
                            }
                            if (entity.Attributes.Contains("spectra_pincode"))
                                shipToPincode = entity.GetAttributeValue<String>("spectra_pincode");
                            string SiteAddress = entity.GetAttributeValue<String>("onl_address");
                            //City domain based on sitecity and product 
                            QueryExpression querydom = new QueryExpression();
                            querydom.EntityName = "alletech_domain";
                            querydom.ColumnSet.AddColumns("alletech_name", "alletech_domain_id");
                            querydom.Criteria.AddCondition("spectra_productsegment", ConditionOperator.Equal, Productsegmentid);
                            querydom.Criteria.AddCondition("pcl_city", ConditionOperator.Equal, sitecity);
                            EntityCollection result1 = service.RetrieveMultiple(querydom);
                            Entity domainentity = result1.Entities[0];
                            if (domainentity.Attributes.Contains("alletech_domain_id"))
                                childDomainId = domainentity.Attributes["alletech_domain_id"].ToString();

                            #region Reevenue group Entity record
                            QueryExpression queryConfig = new QueryExpression("alletech_revenuegroup");
                            queryConfig.ColumnSet = new ColumnSet(true);
                            queryConfig.Criteria.AddCondition("spectra_productsegment", ConditionOperator.Equal, Productsegmentid);
                            queryConfig.Criteria.AddCondition("alletech_name", ConditionOperator.Equal, sitecityname);
                            EntityCollection RevenuegroupCollection = service.RetrieveMultiple(queryConfig);

                            Entity Rev = RevenuegroupCollection.Entities[0];
                            if (RevenuegroupCollection.Entities.Count > 0)
                            {
                                // Entity Rev = RevenuegroupCollection.Entities[0]; 
                                if (Rev.Attributes.Contains("alletech_id"))
                                {
                                    CafInsCityRevGrpId = Rev.GetAttributeValue<string>("alletech_id");
                                }
                                if (Rev.Attributes.Contains("alletech_revenuecoagroupid"))
                                {
                                    coaGroupNo = Rev.GetAttributeValue<int>("alletech_revenuecoagroupid").ToString();
                                }
                                if (Rev.Attributes.Contains("alletech_coano"))
                                {
                                    coaNo = Rev.GetAttributeValue<int>("alletech_coano").ToString();
                                }
                                if (Rev.Attributes.Contains("alletech_ledgerbookno"))
                                {
                                    ledgerBookNo = Rev.GetAttributeValue<int>("alletech_ledgerbookno").ToString();
                                }
                                if (Rev.Attributes.Contains("alletech_ledgerparentid"))
                                {
                                    ledgerParentId = Rev.GetAttributeValue<int>("alletech_ledgerparentid").ToString();
                                }

                                if (Rev.Attributes.Contains("alletech_placeofconsumptionid"))
                                {
                                    placeOfConsumptionId = Rev.GetAttributeValue<int>("alletech_placeofconsumptionid").ToString();

                                }
                                if (Rev.Attributes.Contains("alletech_receiptledgeraccountno"))
                                {
                                    receiptLedgerAccountNo = Rev.GetAttributeValue<int>("alletech_receiptledgeraccountno").ToString();
                                }
                            }
                            #endregion

                            var sitename = entity.GetAttributeValue<String>("spectra_customername");
                            string[] ssizefullnmae = sitename.Split(null);
                            if (ssizefullnmae.Length > 1)
                            {
                                firstName = ssizefullnmae[0];
                                for (int k = 1; k < ssizefullnmae.Length; k++)
                                {
                                    lastnameAccShip = lastnameAccShip + " " + ssizefullnmae[k];
                                }
                            }
                            else
                                firstName = ssizefullnmae[0];

                            #endregion

                            #region start xml  
                            requestXml = "";
                            requestXml += "<CRMCAFRequest>" +
                            "<CAF_No>" + SafNo + "</CAF_No>" +
                            "<CAN_No>" + CanNo + "</CAN_No>";
                            if (rowcount == 1)
                            {
                                #region OrganisationRequest & ChildOrganisationRequest
                                requestXml += "<OrganisationRequest>" +
                                "<customer>true</customer>" +
                                "<domain>" + childDomainId + "</domain>" +
                                "<name>" + childName + "</name>" +
                                "<shortName>" + paccshort + "</shortName>" +
                                "<revenueGroupId>" + CafInsCityRevGrpId + "</revenueGroupId>" +
                                "<receiptLedgerAccountNo>" + receiptLedgerAccountNo + "</receiptLedgerAccountNo>" +
                                "<societyName>"+ Buildingname + "</societyName>" +
                                "<areaName>"+ areaname + "</areaName>" +
                                "<societyFieldId>2</societyFieldId>" +
                                "<areaFieldId>3</areaFieldId>" +
                                "<productSegmentNo>6</productSegmentNo>" +
                                "<verticalSegmentNo>8</verticalSegmentNo>" +
                                "<industryTypeNo>" + industryname + "</industryTypeNo>" +
                                "</OrganisationRequest>";
                               


                                requestXml += "<ChildOrganisationRequest>" +
                                "<customer>true</customer>" +
                                "<domain>" + childDomainId + "</domain>" +
                                "<name>" + childName + "</name>" +
                                "<shortName>" + paccshort + "-0" + rowcount + "</shortName>" +
                                "<revenueGroupId>" + CafInsCityRevGrpId + "</revenueGroupId>" +
                                "<receiptLedgerAccountNo>" + receiptLedgerAccountNo + "</receiptLedgerAccountNo>" +
                               "<societyName>" + Buildingname + "</societyName>" +
                                "<areaName>" + areaname + "</areaName>" +
                                "<societyFieldId>2</societyFieldId>" +
                                "<areaFieldId>3</areaFieldId>" +
                                "<productSegmentNo>6</productSegmentNo>" +
                                "<verticalSegmentNo>8</verticalSegmentNo>" +
                                "<industryTypeNo>" + industryname + "</industryTypeNo>" +
                                "</ChildOrganisationRequest>";
                                #endregion
                            }
                            else
                            {
                                System.Threading.Thread.Sleep(8000);//Just Wait for 5 seconds for  unify response  
                                Entity Parent_accountid = service.Retrieve("opportunity", opportunityid, new ColumnSet("alletech_accountid"));
                                string ExistingCanNo = Parent_accountid.GetAttributeValue<String>("alletech_accountid");//Existing can id on behalf of opportunity id

                                string  account_unifyparentno = GetParentNo(service, ExistingCanNo);//Getting Unify Account No(parent no) on behalf of account

                                requestXml += "<ChildOrganisationRequest>" +
                               "<customer>true</customer>" +
                               "<domain>" + childDomainId + "</domain>" +
                               "<name>" + childName + "</name>" +
                               "<parentNo>" + account_unifyparentno + "</parentNo>" +
                               "<shortName>" + paccshort + "-0" + rowcount + "</shortName>" +
                               "<revenueGroupId>" + CafInsCityRevGrpId + "</revenueGroupId>" +
                               "<receiptLedgerAccountNo>" + receiptLedgerAccountNo + "</receiptLedgerAccountNo>" +
                              "<societyName>" + Buildingname + "</societyName>" +
                                "<areaName>" + areaname + "</areaName>" +
                               "<societyFieldId>2</societyFieldId>" +
                               "<areaFieldId>3</areaFieldId>" +
                               "<productSegmentNo>6</productSegmentNo>" +
                               "<verticalSegmentNo>8</verticalSegmentNo>" +
                               "<industryTypeNo>" + industryname + "</industryTypeNo>" +
                               "</ChildOrganisationRequest>";
                            }
                            #region Contact details

                            #region Billing
                            requestXml += "<ContactDetails>" +
                            "<cityNo>" + billCityId + "</cityNo>" +
                            "<contactTypeNo>1</contactTypeNo>" +
                            "<firstName>" + firstnameAccShip + "</firstName>";
                            if (!String.IsNullOrEmpty(lastName))
                                requestXml = requestXml + "<lastName>" + lastName + "</lastName>";
                            requestXml += "<salutationNo>" + salutation + "</salutationNo>" +
                            "<pin>" + billToPincode + "</pin>" +
                            "<state>" + billToState + "</state>" +
                            "<street>" + billtostreetcomplete + "</street>" +
                            "</ContactDetails>" +
                            #endregion

                            #region  Shipping Address
                                "<ContactDetails>" +
                           "<cityNo>" + shipsitecity + "</cityNo>" +
                            "<contactTypeNo>2</contactTypeNo>" +
                            "<firstName>" + firstName + "</firstName>";

                            if (!String.IsNullOrEmpty(lastnameAccShip))
                                requestXml = requestXml + "<lastName>" + lastnameAccShip + "</lastName>";

                            requestXml = requestXml +
                            "<salutationNo>" + salutation + "</salutationNo>" +
                            "<pin>" + shipToPincode + "</pin>" +
                            "<state>" + shipToState + "</state>" +
                            "<street>" + SiteAddress + "</street>" +
                            "</ContactDetails>";
                            #endregion

                            #endregion


                            #region Bill  to communication
                            if (Email != null)
                            {
                                requestXml = requestXml + "<commMode>" +
                                "<commTypeNo>4</commTypeNo>" +
                                //"<contactNo></contactNo>" +     //  ----- bill email
                                "<isDefault>false</isDefault>" +
                                "<dnc>false</dnc>" +
                                "<ident>" + Email + "</ident>" +
                                "<contactType>BillTo</contactType>" +
                                "</commMode>";
                            }

                            if (Mobile != "")
                            {
                                requestXml = requestXml +
                                "<commMode>" + "<commTypeNo>2</commTypeNo>" +
                                // "<contactNo></contactNo>" +     //  ----- bill phone
                                "<isDefault>false</isDefault>" +
                                "<dnc>false</dnc>" +
                                "<ident>" + Mobile + "</ident>" +
                                "<contactType>BillTo</contactType>" +
                                "</commMode>";
                            }

                            if (Facebook != "")
                            {
                                requestXml = requestXml +
                                "<commMode>" +
                                "<commTypeNo>8</commTypeNo>" +
                                //"<contactNo></contactNo>" +     //  ----- bill to fb
                                "<isDefault>false</isDefault>" +
                                "<dnc>false</dnc>" +
                                "<ident>" + Facebook + "</ident>" +
                                "<contactType>BillTo</contactType>" +
                                "</commMode>";
                            }

                            if (Twitter != "")
                            {
                                requestXml = requestXml +
                                "<commMode>" +
                                "<commTypeNo>7</commTypeNo>" +
                                //"<contactNo></contactNo>" +     //  ----- bill to twitter
                                "<isDefault>false</isDefault>" +
                                "<dnc>false</dnc>" +
                                "<ident>" + Twitter + "</ident>" +
                                "<contactType>BillTo</contactType>" +
                                "</commMode>";
                            }
                            #endregion

                            #region Ship to communication
                            if (Email != "")
                            {
                                requestXml = requestXml +
                                "<commMode>" +
                                "<commTypeNo>4</commTypeNo>" +
                                //"<contactNo></contactNo>" +     //  ----- ship to email
                                "<isDefault>false</isDefault>" +
                                "<dnc>false</dnc>" +
                                "<ident>" + entity.GetAttributeValue<String>("onl_customeremailaddress") + "</ident>" +
                                "<contactType>ShipTo</contactType>" +
                                "</commMode>";
                            }

                            if (Mobile != "")
                            {
                                requestXml = requestXml +
                                "<commMode>" +
                                "<commTypeNo>2</commTypeNo>" +
                                //"<contactNo></contactNo>" +     //  ----- ship to phone
                                "<isDefault>false</isDefault>" +
                                "<dnc>false</dnc>" +
                                "<ident>" + entity.GetAttributeValue<String>("onl_ol_customercontactnumber") + "</ident>" +
                                "<contactType>ShipTo</contactType>" +
                                "</commMode>";
                            }
                            #endregion

                            #region Ledger request 
                            requestXml = requestXml +
                            "<LedgerRequest>" +
                            "<coaGroupNo>" + coaGroupNo + "</coaGroupNo>" +
                            "<coaNo>" + coaNo + "</coaNo>" +
                            "<currencyISO>INR</currencyISO>" +
                            "<ledgerAccountTypeNo>3</ledgerAccountTypeNo>" +
                            "<ledgerBookNo>" + ledgerBookNo + "</ledgerBookNo>" +
                            "<name>" + childName + "</name>" +
                            "<parentId>" + ledgerParentId + "</parentId>" +

                            "<paymentAmnt>" + paymentAmount + "</paymentAmnt>" +
                            "<securityAmnt>" + securityAmount + "</securityAmnt>" +
                            "<paymentType>" + paymentType + "</paymentType>" +
                            "<depositType>" + securityDepositType + "</depositType>" +
                            "<paymentDate>" + paymentDate + "</paymentDate>" +
                            "<chequeNo>" + chequeNo + "</chequeNo>" +
                            "<bankNo>" + bankNo + "</bankNo>" +
                            "<bankBranch>" + banBranch + "</bankBranch>" +
                            "<gst>" + Gst + "</gst>" +
                            "<pan>" + Pan + "</pan>" +
                            "<tan>" + Tan + "</tan>" +
                            "</LedgerRequest>" +
                            #endregion

                            #region serviceGroup 
                                "<serviceGroup>" +
                            "<actcat>2</actcat>" +
                            "<actid>" + CanNo + "</actid>" +
                            "<actname>" + childName + "</actname>" +
                            "<domno>" + childDomainId + "</domno>" +
                            "<placeOfConsumptionId>" + Rev.GetAttributeValue<int>("alletech_placeofconsumptionid").ToString() + "</placeOfConsumptionId>" +
                            "</serviceGroup>" +
                            #endregion

                            #region SessionObject
                    "<SessionObject>" +
                            "<credentialId>1</credentialId>" +
                            "<ipAddress>22.23.25.12</ipAddress>" +
                            "<source>CRM</source>" +
                            "<userName>crm.admin</userName>" +
                            "<userType>123</userType>" +
                            "<usrNo>10651</usrNo>" +
                            "</SessionObject>" +
                            "</CRMCAFRequest>";
                            #endregion


                            if (requestXml.Contains("&"))
                            {
                                requestXml = requestXml.Replace("&", "&amp;");
                            }


                            var B2Buri = new Uri("http://jbossuat.spectranet.in:9001/rest/getCustomer/");
                            var uri = B2Buri;

                            Byte[] requestByte = Encoding.UTF8.GetBytes(requestXml);

                            WebRequest request = WebRequest.Create(uri);
                            request.Method = WebRequestMethods.Http.Post;
                            request.ContentLength = requestByte.Length;
                            request.ContentType = "text/xml; encoding='utf-8'";
                            request.GetRequestStream().Write(requestByte, 0, requestByte.Length);

                            // 
                            #region Response validation
                            try
                            {
                                if (context.Depth == 1)
                                {
                                    using (var response = request.GetResponse())
                                    {
                                        XmlDocument xmlDoc = new XmlDocument();
                                        xmlDoc.Load(response.GetResponseStream());
                                        string tmp = xmlDoc.InnerXml.ToString();

                                        string CAF_No, CAN_No, Code, Message;
                                        #region To create Integration Log from Response
                                        XmlNodeList node1 = xmlDoc.GetElementsByTagName("CRMCAFResponse");
                                        for (int i = 0; i <= node1.Count - 1; i++)
                                        {
                                            CAF_No = node1[i].ChildNodes.Item(0).InnerText.Trim();
                                            CAN_No = node1[i].ChildNodes.Item(1).InnerText.Trim();
                                            Code = node1[i].ChildNodes.Item(2).InnerText.Trim();
                                            Message = node1[i].ChildNodes.Item(3).InnerText.Trim();
                                            Entity IntegrationLog = new Entity("alletech_integrationlog_enterprise");
                                            IntegrationLog["alletech_cafno"] = CAF_No;
                                            IntegrationLog["alletech_canno"] = CAN_No;
                                            IntegrationLog["alletech_code"] = Code;
                                            IntegrationLog["alletech_message"] = Message;

                                            IntegrationLog["alletech_name"] = "Approved_" + CAF_No + "_" + CAN_No;

                                            IntegrationLog["alletech_responsetype"] = new OptionSetValue(2);
                                            IntegrationLog["alletech_approvalrequest"] = requestXml;
                                            Guid Siteguid = entity.Id;
                                            Guid safid = SAF.Id;
                                            //add site lookup 
                                            IntegrationLog["spectra_siteidid"] = new EntityReference("onl_customersite", Siteguid);
                                            // IntegrationLog["alletech_can"] = new EntityReference("onl_saf", SAF.Id);
                                            IntegrationLog["onl_safid"] = new EntityReference("onl_saf", safid);
                                            service.Create(IntegrationLog);
                                            flag = true;
                                        }
                                        #endregion
                                    }
                                    if (flag == true)
                                    {
                                        #region Update Customer Site Details 
                                        Entity Sites = service.Retrieve("onl_customersite", entity.Id, new ColumnSet("spectra_parentaccout", "spectra_siteaccountno", "spectra_siteresponse", "spectra_siteuserpassword"));

                                        ConditionExpression conditionacc = new ConditionExpression("alletech_accountid", ConditionOperator.Equal, CanNo);
                                        FilterExpression filteracc = new FilterExpression();
                                        filteracc.AddCondition(conditionacc);
                                        filteracc.FilterOperator = LogicalOperator.And;
                                        QueryExpression queryacc = new QueryExpression
                                        {
                                            EntityName = "account",
                                            ColumnSet = new ColumnSet(true),
                                            Criteria = filter,
                                        };
                                        EntityCollection childAccountC = service.RetrieveMultiple(queryacc);
                                        if (childAccountC.Entities.Count == 0)
                                            return;
                                        Entity childPAccount = childAccountC.Entities[0];
                                        Guid pacid = childPAccount.Id;

                                        Sites["spectra_parentaccout"] = new EntityReference("account", pacid);
                                        Sites["spectra_siteaccountno"] = CanNo;
                                        Sites["spectra_siteresponse"] = "true";
                                        Sites["spectra_siteuserpassword"] = CreateRandomPasswordWithRandomLength(8);
                                        service.Update(Sites);
                                        #endregion
                                    }
                                    else
                                    {
                                        Entity Sites = service.Retrieve("onl_customersite", entity.Id, new ColumnSet("spectra_siteaccountno", "spectra_siteresponse"));
                                        Sites["spectra_siteaccountno"] = CanNo;
                                        Sites["spectra_siteresponse"] = "false";
                                        service.Update(Sites);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                throw new InvalidPluginExecutionException(ex.Message);
                            }
                            #endregion
                            #endregion
                            rowcount++;

                        }
                        if (context.Depth == 1)
                        {
                            Entity _saf = new Entity("onl_saf");
                            _saf.Id = SAF.Id;
                            _saf["onl_status"] = new OptionSetValue(122050003);
                            // _saf["onl_orgcreationresponseonl"] = true;
                            service.Update(_saf);
                        }
                        #endregion
                        //}
                        //else
                        //{
                        //    //New Sites Created 
                        //}
                    }
                    //}
                }
            }
            catch (Exception e)
            {
                throw new InvalidPluginExecutionException(e.Message);
            }
        }
        public static string GetParentNo(IOrganizationService service, string AccountCanId)
        {
            string parentnoid = string.Empty;
            ConditionExpression conditionp = new ConditionExpression("alletech_accountid", ConditionOperator.Equal, AccountCanId);
            FilterExpression filter = new FilterExpression();
            filter.AddCondition(conditionp);
            filter.FilterOperator = LogicalOperator.And;
            QueryExpression queryparent = new QueryExpression
            {
                EntityName = "account",
                ColumnSet = new ColumnSet(true),
                Criteria = filter,
            };
            EntityCollection ParentAccEc = service.RetrieveMultiple(queryparent);

            if (ParentAccEc.Entities.Count > 0)
            {
                Entity ChildParentAccount = ParentAccEc.Entities[0];
                parentnoid = ChildParentAccount.GetAttributeValue<string>("alletech_accountno");
            }
            return parentnoid;
        }

        private static string CreateRandomPasswordWithRandomLength(int Length)
        {
            // Create a string of characters, numbers, special characters that allowed in the password  
            string validChars = "ABCDEFGHJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*?_-";
            Random random = new Random();
                     
            char[] chars = new char[Length];
            for (int i = 0; i < Length; i++)
            {
                chars[i] = validChars[random.Next(0, validChars.Length)];
            }
            return new string(chars);
        }
        public static string GetAccountNo(IOrganizationService service, Guid Accountkey)
        {
            string getAccno = string.Empty;
            try
            {

                ConditionExpression condition = new ConditionExpression("accountid", ConditionOperator.Equal, Accountkey);
                FilterExpression filter = new FilterExpression();
                filter.AddCondition(condition);
                filter.FilterOperator = LogicalOperator.And;
                QueryExpression query = new QueryExpression
                {
                    EntityName = "account",
                    ColumnSet = new ColumnSet("alletech_accountid"),
                    Criteria = filter,
                };
                EntityCollection ecAccount = service.RetrieveMultiple(query);
                if (ecAccount.Entities.Count > 0)
                {
                    Entity childAccount = ecAccount.Entities[0];
                    getAccno = childAccount.GetAttributeValue<string>("alletech_accountid");
                }
            }
            catch (Exception e)
            {
                throw e;
            }
            return getAccno;
        }
        public void ParentAccountCreation(IOrganizationService service, ref Entity SAF, Guid Oppid, IPluginExecutionContext context, String canId)
        {
            if (context.Depth == 1)
            {
                EntityReference childAccountDomain = null;
                ConditionExpression condition = new ConditionExpression("alletech_accountid", ConditionOperator.Equal, canId);
                FilterExpression filter = new FilterExpression();
                filter.AddCondition(condition);
                filter.FilterOperator = LogicalOperator.And;
                QueryExpression query = new QueryExpression
                {
                    EntityName = "account",
                    ColumnSet = new ColumnSet(true),
                    Criteria = filter,
                };
                EntityCollection childAccountCollection = service.RetrieveMultiple(query);
                if (childAccountCollection.Entities.Count == 0)
                    return;
                Entity childAccount = childAccountCollection.Entities[0];

                // check if Parent account, existing on Child has Unify ID
                if (!childAccount.Attributes.Contains("parentaccountid"))
                {
                    Entity ChildParentAccount = new Entity("account");

                    if (childAccount.Attributes.Contains("alletech_domain"))
                    {
                        tracingService.Trace("child account contains domain");
                        ChildParentAccount["alletech_domain"] = childAccount.GetAttributeValue<EntityReference>("alletech_domain");
                        childAccountDomain = childAccount.GetAttributeValue<EntityReference>("alletech_domain");
                        tracingService.Trace("domain : " + childAccountDomain.Name);
                    }
                    else
                    {

                        EntityReference cityid = childAccount.GetAttributeValue<EntityReference>("alletech_city");

                        childAccountDomain = retrieveDomain(cityid.Id, service, Productsegmentid);

                        if (childAccountDomain != null)
                        {
                            ChildParentAccount["alletech_domain"] = childAccountDomain;
                            tracingService.Trace("domain : " + childAccountDomain.Id);
                        }
                        else
                        {
                            throw new InvalidPluginExecutionException("Domain record not found");
                        }
                    }

                    #region Account Information Details

                    tracingService.Trace("Before Account Info");


                    if (childAccount.Attributes.Contains("alletech_salutation"))
                        ChildParentAccount["alletech_salutation"] = childAccount.GetAttributeValue<OptionSetValue>("alletech_salutation");
                    if (childAccount.Attributes.Contains("alletech_subbusinesssegment"))
                        ChildParentAccount["alletech_subbusinesssegment"] = childAccount.GetAttributeValue<EntityReference>("alletech_subbusinesssegment");
                    if (childAccount.Attributes.Contains("alletech_amountcharged"))
                        ChildParentAccount["alletech_amountcharged"] = childAccount.GetAttributeValue<Money>("alletech_amountcharged");
                    if (childAccount.Attributes.Contains("alletech_businesssegment"))
                        ChildParentAccount["alletech_businesssegment"] = childAccount.GetAttributeValue<EntityReference>("alletech_businesssegment");
                    if (childAccount.Attributes.Contains("alletech_industry"))
                        ChildParentAccount["alletech_industry"] = childAccount.GetAttributeValue<EntityReference>("alletech_industry");
                    if (childAccount.Attributes.Contains("alletech_firmtype"))
                        ChildParentAccount["alletech_firmtype"] = childAccount.GetAttributeValue<OptionSetValue>("alletech_firmtype");
                    if (childAccount.Attributes.Contains("alletech_activationdate"))
                        ChildParentAccount["alletech_activationdate"] = childAccount.GetAttributeValue<DateTime>("alletech_activationdate");
                    if (childAccount.Attributes.Contains("alletech_channelpartner"))
                        ChildParentAccount["alletech_channelpartner"] = childAccount.GetAttributeValue<EntityReference>("alletech_channelpartner");
                    if (childAccount.Attributes.Contains("alletech_product"))
                        ChildParentAccount["alletech_product"] = childAccount.GetAttributeValue<EntityReference>("alletech_product");
                    if (childAccount.Attributes.Contains(""))

                        tracingService.Trace("before string attributes");

                    if (childAccount.Attributes.Contains("name"))
                        ChildParentAccount["name"] = childAccount.GetAttributeValue<String>("name");
                    //if (childAccount.Attributes.Contains("alletech_unifyshortname"))
                    //    ChildParentAccount["alletech_unifyshortname"] = childAccount.GetAttributeValue<String>("alletech_unifyshortname");
                    if (childAccount.Attributes.Contains("alletech_accountshortname"))
                        ChildParentAccount["alletech_accountshortname"] = childAccount.GetAttributeValue<String>("alletech_accountshortname");
                    if (childAccount.Attributes.Contains("emailaddress1"))
                        ChildParentAccount["emailaddress1"] = childAccount.GetAttributeValue<String>("emailaddress1");
                    if (childAccount.Attributes.Contains("alletech_transactionid"))
                        ChildParentAccount["alletech_transactionid"] = childAccount.GetAttributeValue<String>("alletech_transactionid");
                    if (childAccount.Attributes.Contains("alletech_emailid"))
                        ChildParentAccount["alletech_emailid"] = childAccount.GetAttributeValue<String>("alletech_emailid");
                    if (childAccount.Attributes.Contains("alletech_companyname"))
                        ChildParentAccount["alletech_companyname"] = childAccount.GetAttributeValue<String>("alletech_companyname");
                    if (childAccount.Attributes.Contains("alletech_facebookid"))
                        ChildParentAccount["alletech_facebookid"] = childAccount.GetAttributeValue<String>("alletech_facebookid");
                    if (childAccount.Attributes.Contains("alletech_companynamehome"))
                        ChildParentAccount["alletech_companynamehome"] = childAccount.GetAttributeValue<String>("alletech_companynamehome");
                    if (childAccount.Attributes.Contains("alletech_twitterid"))
                        ChildParentAccount["alletech_twitterid"] = childAccount.GetAttributeValue<String>("alletech_twitterid");
                    if (childAccount.Attributes.Contains("alletech_paymentid"))
                        ChildParentAccount["alletech_paymentid"] = childAccount.GetAttributeValue<String>("alletech_paymentid");
                    if (childAccount.Attributes.Contains("alletech_mobilephone"))
                        ChildParentAccount["alletech_mobilephone"] = childAccount.GetAttributeValue<String>("alletech_mobilephone");
                    if (childAccount.Attributes.Contains("telephone1"))
                        ChildParentAccount["telephone1"] = childAccount.GetAttributeValue<String>("telephone1");

                    if (childAccount.Attributes.Contains("websiteurl"))
                        ChildParentAccount["websiteurl"] = childAccount.GetAttributeValue<String>("websiteurl");
                   

                    tracingService.Trace("after string attributes");
                    #endregion

                    #region Account Address

                    tracingService.Trace(" In Account Address attributes");
                    #region New Added Code
                    if (childAccount.Attributes.Contains("primarycontactid"))
                        ChildParentAccount["primarycontactid"] = childAccount.GetAttributeValue<EntityReference>("primarycontactid");
                    if (childAccount.Attributes.Contains("alletech_ship_specifyarea"))
                        ChildParentAccount["alletech_ship_specifyarea"] = childAccount.GetAttributeValue<string>("alletech_ship_specifyarea");
                    if (childAccount.Attributes.Contains("alletech_ship_phonetypeno"))
                        ChildParentAccount["alletech_ship_phonetypeno"] = childAccount.GetAttributeValue<int>("alletech_ship_phonetypeno");
                    if (childAccount.Attributes.Contains("alletech_ship_emailtypeno"))
                        ChildParentAccount["alletech_ship_emailtypeno"] = childAccount.GetAttributeValue<int>("alletech_ship_emailtypeno");
                    if (childAccount.Attributes.Contains("alletech_buildingname"))
                        ChildParentAccount["alletech_buildingname"] = childAccount.GetAttributeValue<EntityReference>("alletech_buildingname");
                    if (childAccount.Attributes.Contains("alletech_ship_specifybuilding"))
                        ChildParentAccount["alletech_ship_specifybuilding"] = childAccount.GetAttributeValue<string>("alletech_ship_specifybuilding");
                    if (childAccount.Attributes.Contains("alletech_ship_contactid"))
                        ChildParentAccount["alletech_ship_contactid"] = childAccount.GetAttributeValue<string>("alletech_ship_contactid");
                    #endregion

                    tracingService.Trace("after newly added code in Account address");
                    if (childAccount.Attributes.Contains("alletech_buildingnoplotno"))
                        ChildParentAccount["alletech_buildingnoplotno"] = childAccount.GetAttributeValue<String>("alletech_buildingnoplotno");
                    if (childAccount.Attributes.Contains("alletech_blocknumbertowernumber"))
                        ChildParentAccount["alletech_blocknumbertowernumber"] = childAccount.GetAttributeValue<String>("alletech_blocknumbertowernumber");
                    if (childAccount.Attributes.Contains("alletech_block"))
                        ChildParentAccount["alletech_block"] = childAccount.GetAttributeValue<String>("alletech_block");
                    if (childAccount.Attributes.Contains("alletech_houseflatnumber"))
                        ChildParentAccount["alletech_houseflatnumber"] = childAccount.GetAttributeValue<String>("alletech_houseflatnumber");
                    if (childAccount.Attributes.Contains("alletech_locality"))
                        ChildParentAccount["alletech_locality"] = childAccount.GetAttributeValue<String>("alletech_locality");
                    if (childAccount.Attributes.Contains("alletech_buildingtype"))
                        ChildParentAccount["alletech_buildingtype"] = childAccount.GetAttributeValue<OptionSetValue>("alletech_buildingtype");
                    if (childAccount.Attributes.Contains("alletech_floor"))
                        ChildParentAccount["alletech_floor"] = childAccount.GetAttributeValue<String>("alletech_floor");
                    if (childAccount.Attributes.Contains("alletech_street"))
                        ChildParentAccount["alletech_street"] = childAccount.GetAttributeValue<String>("alletech_street");
                    if (childAccount.Attributes.Contains("alletech_landmarkifany"))
                        ChildParentAccount["alletech_landmarkifany"] = childAccount.GetAttributeValue<String>("alletech_landmarkifany");
                    if (childAccount.Attributes.Contains("alletech_country"))
                        ChildParentAccount["alletech_country"] = childAccount.GetAttributeValue<EntityReference>("alletech_country");
                    if (childAccount.Attributes.Contains("alletech_state"))
                        ChildParentAccount["alletech_state"] = childAccount.GetAttributeValue<EntityReference>("alletech_state");
                    if (childAccount.Attributes.Contains("alletech_city"))
                        ChildParentAccount["alletech_city"] = childAccount.GetAttributeValue<EntityReference>("alletech_city");
                    if (childAccount.Attributes.Contains("alletech_zippostalcode"))
                        ChildParentAccount["alletech_zippostalcode"] = childAccount.GetAttributeValue<String>("alletech_zippostalcode");
                    if (childAccount.Attributes.Contains("alletech_area"))
                        ChildParentAccount["alletech_area"] = childAccount.GetAttributeValue<EntityReference>("alletech_area");
                    if (childAccount.Attributes.Contains("alletech_lcoarea"))
                        ChildParentAccount["alletech_lcoarea"] = childAccount.GetAttributeValue<String>("alletech_lcoarea");
                    #endregion

                    #region Bill Address

                    tracingService.Trace("In bill Address section");
                    #region New Added Code
                    if (childAccount.Attributes.Contains("alletech_contactname"))
                        ChildParentAccount["alletech_contactname"] = childAccount.GetAttributeValue<string>("alletech_contactname");
                    if (childAccount.Attributes.Contains("alletech_shippingemailid"))
                        ChildParentAccount["alletech_shippingemailid"] = childAccount.GetAttributeValue<string>("alletech_shippingemailid");
                    if (childAccount.Attributes.Contains("alletech_mobilephone2"))
                        ChildParentAccount["alletech_mobilephone2"] = childAccount.GetAttributeValue<string>("alletech_mobilephone2");
                    #endregion

                    if (childAccount.Attributes.Contains("alletech_bill_countrymain"))
                        ChildParentAccount["alletech_bill_countrymain"] = childAccount.GetAttributeValue<EntityReference>("alletech_bill_countrymain");
                    if (childAccount.Attributes.Contains("alletech_bill_statemain"))
                        ChildParentAccount["alletech_bill_statemain"] = childAccount.GetAttributeValue<EntityReference>("alletech_bill_statemain");
                    if (childAccount.Attributes.Contains("alletech_bill_citymain"))
                        ChildParentAccount["alletech_bill_citymain"] = childAccount.GetAttributeValue<EntityReference>("alletech_bill_citymain");
                    if (childAccount.Attributes.Contains("alletech_bill_pincode"))
                        ChildParentAccount["alletech_bill_pincode"] = childAccount.GetAttributeValue<String>("alletech_bill_pincode");
                    if (childAccount.Attributes.Contains("alletech_bill_areamain"))
                        ChildParentAccount["alletech_bill_areamain"] = childAccount.GetAttributeValue<EntityReference>("alletech_bill_areamain");
                    if (childAccount.Attributes.Contains("alletech_bill_specifybillingarea"))
                        ChildParentAccount["alletech_bill_specifybillingarea"] = childAccount.GetAttributeValue<String>("alletech_bill_specifybillingarea");
                    if (childAccount.Attributes.Contains("alletech_bill_buildingname"))
                        ChildParentAccount["alletech_bill_buildingname"] = childAccount.GetAttributeValue<EntityReference>("alletech_bill_buildingname");
                    if (childAccount.Attributes.Contains("alletech_bill_specifybuilding"))
                        ChildParentAccount["alletech_bill_specifybuilding"] = childAccount.GetAttributeValue<String>("alletech_bill_specifybuilding");
                    if (childAccount.Attributes.Contains("alletech_bill_buildingnoplotno"))
                        ChildParentAccount["alletech_bill_buildingnoplotno"] = childAccount.GetAttributeValue<String>("alletech_bill_buildingnoplotno");
                    if (childAccount.Attributes.Contains("alletech_bill_locality"))
                        ChildParentAccount["alletech_bill_locality"] = childAccount.GetAttributeValue<String>("alletech_bill_locality");
                    if (childAccount.Attributes.Contains("alletech_bill_buildingtype"))
                        ChildParentAccount["alletech_bill_buildingtype"] = childAccount.GetAttributeValue<OptionSetValue>("alletech_bill_buildingtype");
                    if (childAccount.Attributes.Contains("alletech_bill_floor"))
                        ChildParentAccount["alletech_bill_floor"] = childAccount.GetAttributeValue<String>("alletech_bill_floor");
                    if (childAccount.Attributes.Contains("alletech_bill_street"))
                        ChildParentAccount["alletech_bill_street"] = childAccount.GetAttributeValue<String>("alletech_bill_street");
                    if (childAccount.Attributes.Contains("alletech_bill_landmarkifany"))
                        ChildParentAccount["alletech_bill_landmarkifany"] = childAccount.GetAttributeValue<String>("alletech_bill_landmarkifany");
                    if (childAccount.Attributes.Contains("alletech_bill_billingphoneno"))
                        ChildParentAccount["alletech_bill_billingphoneno"] = childAccount.GetAttributeValue<String>("alletech_bill_billingphoneno");
                    #endregion

                    tracingService.Trace("Before Create");

                    Guid parentAccountId = service.Create(ChildParentAccount);

                    CreateAccountId = parentAccountId;

                    NewAccountid = GetAccountNo(service, parentAccountId);
                    //Entity SAF01 = service.Retrieve("onl_saf", SAF.Id, new ColumnSet("onl_parentaccountonl"));
                    //SAF01["onl_parentaccountonl"] = new EntityReference("account", parentAccountId);
                    //service.Update(SAF01);

                    //tracingService.Trace("updating reference in postimage");
                    //SAF["onl_parentaccountonl"] = new EntityReference("account", parentAccountId);


                    // Parent Account Association at Child Account
                    Entity childAccount01 = service.Retrieve("account", childAccount.Id, new ColumnSet("parentaccountid"));
                    if (childAccountDomain != null)
                        childAccount01.Attributes["alletech_domain"] = childAccountDomain;
                    childAccount01["parentaccountid"] = new EntityReference("account", parentAccountId);
                    service.Update(childAccount01);

                    tracingService.Trace("After updating CHild with Parent account");
                }
            }

        }
        public String ChildcountSitesCreation(IOrganizationService service, ref Entity SAF, Guid Oppid, IPluginExecutionContext context, int sitecount, string ushortname, string Trigger,Entity SiteEntity)
        {
            string Account2 = string.Empty;
            if (context.Depth == 1)
            {
                EntityReference childAccountDomain = null;
                Entity OppEntity = service.Retrieve("opportunity", Oppid, new ColumnSet("alletech_accountid"));
                String canId = String.Empty;
                canId = OppEntity.GetAttributeValue<String>("alletech_accountid");



                ConditionExpression condition = new ConditionExpression("alletech_accountid", ConditionOperator.Equal, canId);
                FilterExpression filter = new FilterExpression();
                filter.AddCondition(condition);
                filter.FilterOperator = LogicalOperator.And;
                QueryExpression query = new QueryExpression
                {
                    EntityName = "account",
                    ColumnSet = new ColumnSet(true),
                    Criteria = filter,
                };
                EntityCollection childAccountCollection = service.RetrieveMultiple(query);

                Entity childAccount = childAccountCollection.Entities[0];

                if (Trigger == "CreateSiteAccount")
                {
                    // check if Parent account, existing on Child has Unify ID
                    if (childAccount.Attributes.Contains("parentaccountid"))
                    {
                        Entity ChildParentAccount = new Entity("account");

                        EntityReference cityid = SiteEntity.GetAttributeValue<EntityReference>("onl_city");

                        childAccountDomain = retrieveDomain(cityid.Id, service, Productsegmentid);
                        if (childAccountDomain != null)
                        {
                            ChildParentAccount["alletech_domain"] = childAccountDomain;
                            tracingService.Trace("domain : " + childAccountDomain.Id);
                        }
                        //
                        ChildParentAccount["parentaccountid"] = childAccount.GetAttributeValue<EntityReference>("parentaccountid");//new EntityReference("account", parentaid);

                        #region Account Information Details

                        tracingService.Trace("Before Account Info");


                        if (childAccount.Attributes.Contains("alletech_salutation"))
                            ChildParentAccount["alletech_salutation"] = childAccount.GetAttributeValue<OptionSetValue>("alletech_salutation");
                        if (childAccount.Attributes.Contains("alletech_subbusinesssegment"))
                            ChildParentAccount["alletech_subbusinesssegment"] = childAccount.GetAttributeValue<EntityReference>("alletech_subbusinesssegment");
                        if (childAccount.Attributes.Contains("alletech_amountcharged"))
                            ChildParentAccount["alletech_amountcharged"] = childAccount.GetAttributeValue<Money>("alletech_amountcharged");
                        if (childAccount.Attributes.Contains("alletech_businesssegment"))
                            ChildParentAccount["alletech_businesssegment"] = childAccount.GetAttributeValue<EntityReference>("alletech_businesssegment");
                        if (childAccount.Attributes.Contains("alletech_industry"))
                            ChildParentAccount["alletech_industry"] = childAccount.GetAttributeValue<EntityReference>("alletech_industry");
                        if (childAccount.Attributes.Contains("alletech_firmtype"))
                            ChildParentAccount["alletech_firmtype"] = childAccount.GetAttributeValue<OptionSetValue>("alletech_firmtype");
                        if (childAccount.Attributes.Contains("alletech_activationdate"))
                            ChildParentAccount["alletech_activationdate"] = childAccount.GetAttributeValue<DateTime>("alletech_activationdate");
                        if (childAccount.Attributes.Contains("alletech_channelpartner"))
                            ChildParentAccount["alletech_channelpartner"] = childAccount.GetAttributeValue<EntityReference>("alletech_channelpartner");
                        if (childAccount.Attributes.Contains("alletech_product"))
                            ChildParentAccount["alletech_product"] = childAccount.GetAttributeValue<EntityReference>("alletech_product");
                      
                            tracingService.Trace("before s string attributes");

                        if (childAccount.Attributes.Contains("name"))
                            ChildParentAccount["name"] = childAccount.GetAttributeValue<String>("name");
                        //if (childAccount.Attributes.Contains("alletech_unifyshortname"))
                        //    ChildParentAccount["alletech_unifyshortname"] = childAccount.GetAttributeValue<String>("alletech_unifyshortname");
                        if (childAccount.Attributes.Contains("alletech_accountshortname"))
                            ChildParentAccount["alletech_accountshortname"] = childAccount.GetAttributeValue<String>("alletech_accountshortname");
                        if (childAccount.Attributes.Contains("emailaddress1"))
                            ChildParentAccount["emailaddress1"] = childAccount.GetAttributeValue<String>("emailaddress1");
                        if (childAccount.Attributes.Contains("alletech_transactionid"))
                            ChildParentAccount["alletech_transactionid"] = childAccount.GetAttributeValue<String>("alletech_transactionid");
                        if (childAccount.Attributes.Contains("alletech_emailid"))
                            ChildParentAccount["alletech_emailid"] = childAccount.GetAttributeValue<String>("alletech_emailid");
                        if (childAccount.Attributes.Contains("alletech_companyname"))
                            ChildParentAccount["alletech_companyname"] = childAccount.GetAttributeValue<String>("alletech_companyname");
                        if (childAccount.Attributes.Contains("alletech_facebookid"))
                            ChildParentAccount["alletech_facebookid"] = childAccount.GetAttributeValue<String>("alletech_facebookid");
                        if (childAccount.Attributes.Contains("alletech_companynamehome"))
                            ChildParentAccount["alletech_companynamehome"] = childAccount.GetAttributeValue<String>("alletech_companynamehome");
                        if (childAccount.Attributes.Contains("alletech_twitterid"))
                            ChildParentAccount["alletech_twitterid"] = childAccount.GetAttributeValue<String>("alletech_twitterid");
                        if (childAccount.Attributes.Contains("alletech_paymentid"))
                            ChildParentAccount["alletech_paymentid"] = childAccount.GetAttributeValue<String>("alletech_paymentid");
                        if (childAccount.Attributes.Contains("alletech_mobilephone"))
                            ChildParentAccount["alletech_mobilephone"] = childAccount.GetAttributeValue<String>("alletech_mobilephone");
                        if (childAccount.Attributes.Contains("telephone1"))
                            ChildParentAccount["telephone1"] = childAccount.GetAttributeValue<String>("telephone1");

                        if (childAccount.Attributes.Contains("websiteurl"))
                            ChildParentAccount["websiteurl"] = childAccount.GetAttributeValue<String>("websiteurl");
                        //if (SiteEntity.Attributes.Contains("onl_address"))
                        //    ChildParentAccount["alletech_address"] = SiteEntity.GetAttributeValue<String>("onl_address");

                        tracingService.Trace("after site string attributes");
                        #endregion
                        string firstName = string.Empty;
                        string lastnameAccShip = string.Empty;
                        var sitename = SiteEntity.GetAttributeValue<String>("spectra_customername");
                        string[] ssizefullnmae = sitename.Split(null);
                        if (ssizefullnmae.Length > 1)
                        {
                            firstName = ssizefullnmae[0];
                            for (int k = 1; k < ssizefullnmae.Length; k++)
                            {
                                lastnameAccShip = lastnameAccShip + " " + ssizefullnmae[k];
                            }
                        }
                        else
                            firstName = ssizefullnmae[0];

                        if (childAccount.Attributes.Contains("alletech_contactfirstname"))
                            ChildParentAccount["alletech_contactfirstname"] = firstName;
                        if (childAccount.Attributes.Contains("alletech_contactlastname"))
                            ChildParentAccount["alletech_contactlastname"] = lastnameAccShip;
                        #region Account Address

                        tracingService.Trace(" In site Account Address attributes");
                        #region New Added Code
                        //if (childAccount.Attributes.Contains("primarycontactid"))
                        //    ChildParentAccount["primarycontactid"] = childAccount.GetAttributeValue<EntityReference>("primarycontactid");
                      
                        //if (SiteEntity.Attributes.Contains("onl_ol_customercontactnumber"))
                        //    ChildParentAccount["alletech_ship_phonetypeno"] = SiteEntity.GetAttributeValue<int>("onl_ol_customercontactnumber");
                        //if (childAccount.Attributes.Contains("alletech_ship_emailtypeno"))
                        //    ChildParentAccount["alletech_ship_emailtypeno"] = childAccount.GetAttributeValue<int>("alletech_ship_emailtypeno");
                      
                        //if (childAccount.Attributes.Contains("alletech_ship_contactid"))
                        //    ChildParentAccount["alletech_ship_contactid"] = childAccount.GetAttributeValue<string>("alletech_ship_contactid");
                        #endregion

                        tracingService.Trace("after site newly added code in Account address");

                        if (SiteEntity.Attributes.Contains("onl_address"))
                            ChildParentAccount["alletech_street"] = SiteEntity.GetAttributeValue<String>("onl_address");

                        if (SiteEntity.Attributes.Contains("alletech_state"))
                            ChildParentAccount["alletech_state"] = SiteEntity.GetAttributeValue<EntityReference>("onl_state");
                        if (SiteEntity.Attributes.Contains("onl_city"))
                            ChildParentAccount["alletech_city"] = SiteEntity.GetAttributeValue<EntityReference>("onl_city");
                        if (SiteEntity.Attributes.Contains("spectra_pincode"))
                            ChildParentAccount["alletech_zippostalcode"] = SiteEntity.GetAttributeValue<String>("spectra_pincode");

                        #endregion

                        #region Bill Address

                        tracingService.Trace("In bill Address section");
                        #region New Added Code
                        if (childAccount.Attributes.Contains("alletech_contactname"))
                            ChildParentAccount["alletech_contactname"] = childAccount.GetAttributeValue<string>("alletech_contactname");
                        if (childAccount.Attributes.Contains("alletech_shippingemailid"))
                            ChildParentAccount["alletech_shippingemailid"] = childAccount.GetAttributeValue<string>("alletech_shippingemailid");
                        if (childAccount.Attributes.Contains("alletech_mobilephone2"))
                            ChildParentAccount["alletech_mobilephone2"] = childAccount.GetAttributeValue<string>("alletech_mobilephone2");
                        #endregion

                        if (childAccount.Attributes.Contains("alletech_bill_countrymain"))
                            ChildParentAccount["alletech_bill_countrymain"] = childAccount.GetAttributeValue<EntityReference>("alletech_bill_countrymain");
                        if (childAccount.Attributes.Contains("alletech_bill_statemain"))
                            ChildParentAccount["alletech_bill_statemain"] = childAccount.GetAttributeValue<EntityReference>("alletech_bill_statemain");
                        if (childAccount.Attributes.Contains("alletech_bill_citymain"))
                            ChildParentAccount["alletech_bill_citymain"] = childAccount.GetAttributeValue<EntityReference>("alletech_bill_citymain");
                        if (childAccount.Attributes.Contains("alletech_bill_pincode"))
                            ChildParentAccount["alletech_bill_pincode"] = childAccount.GetAttributeValue<String>("alletech_bill_pincode");
                        if (childAccount.Attributes.Contains("alletech_bill_areamain"))
                            ChildParentAccount["alletech_bill_areamain"] = childAccount.GetAttributeValue<EntityReference>("alletech_bill_areamain");
                        if (childAccount.Attributes.Contains("alletech_bill_specifybillingarea"))
                            ChildParentAccount["alletech_bill_specifybillingarea"] = childAccount.GetAttributeValue<String>("alletech_bill_specifybillingarea");
                        if (childAccount.Attributes.Contains("alletech_bill_buildingname"))
                            ChildParentAccount["alletech_bill_buildingname"] = childAccount.GetAttributeValue<EntityReference>("alletech_bill_buildingname");
                        if (childAccount.Attributes.Contains("alletech_bill_specifybuilding"))
                            ChildParentAccount["alletech_bill_specifybuilding"] = childAccount.GetAttributeValue<String>("alletech_bill_specifybuilding");
                        if (childAccount.Attributes.Contains("alletech_bill_buildingnoplotno"))
                            ChildParentAccount["alletech_bill_buildingnoplotno"] = childAccount.GetAttributeValue<String>("alletech_bill_buildingnoplotno");
                        if (childAccount.Attributes.Contains("alletech_bill_locality"))
                            ChildParentAccount["alletech_bill_locality"] = childAccount.GetAttributeValue<String>("alletech_bill_locality");
                        if (childAccount.Attributes.Contains("alletech_bill_buildingtype"))
                            ChildParentAccount["alletech_bill_buildingtype"] = childAccount.GetAttributeValue<OptionSetValue>("alletech_bill_buildingtype");
                        if (childAccount.Attributes.Contains("alletech_bill_floor"))
                            ChildParentAccount["alletech_bill_floor"] = childAccount.GetAttributeValue<String>("alletech_bill_floor");
                        if (childAccount.Attributes.Contains("alletech_bill_street"))
                            ChildParentAccount["alletech_bill_street"] = childAccount.GetAttributeValue<String>("alletech_bill_street");
                        if (childAccount.Attributes.Contains("alletech_bill_landmarkifany"))
                            ChildParentAccount["alletech_bill_landmarkifany"] = childAccount.GetAttributeValue<String>("alletech_bill_landmarkifany");
                        if (childAccount.Attributes.Contains("alletech_bill_billingphoneno"))
                            ChildParentAccount["alletech_bill_billingphoneno"] = childAccount.GetAttributeValue<String>("alletech_bill_billingphoneno");
                        #endregion

                        tracingService.Trace("Before account Create");

                        Guid ChildParentAccountId = service.Create(ChildParentAccount);



                        Account2 = GetAccountNo(service, ChildParentAccountId);

                        // Parent Account Association at Child Account
                        #region Parent Account update Shortname

                       
                        Entity childAccount01 = service.Retrieve("account", childAccount.Id, new ColumnSet("parentaccountid", "alletech_unifyshortname"));

                        //childAccount01["parentaccountid"] = new EntityReference("account", parentaid);

                        string paccshort = ushortname.Split('-')[0];
                        childAccount01["alletech_unifyshortname"] = paccshort + "-0" + sitecount;

                        service.Update(childAccount01);


                        #endregion
                    }
                }

            }
            return Account2;
        }

        public void ChildAccountUpdation(IOrganizationService service, Entity SAFID, Guid Oppid, IPluginExecutionContext context)
        {

            try
            {
                //if (context.Depth == 1)
                //{
                Entity OppEntity = service.Retrieve("opportunity", Oppid, new ColumnSet("alletech_accountid"));

                var accountid = OppEntity.GetAttributeValue<String>("alletech_accountid");

                QueryExpression queryConfig = new QueryExpression("account");
                queryConfig.ColumnSet = new ColumnSet(true);
                queryConfig.Criteria.AddCondition("alletech_accountid", ConditionOperator.Equal, accountid);
                EntityCollection RevenuegroupCollection = service.RetrieveMultiple(queryConfig);

                if (RevenuegroupCollection.Entities.Count > 0)
                {
                    Entity childAccount = RevenuegroupCollection.Entities[0];

                    Entity SAF = service.Retrieve("onl_saf", SAFID.Id, new ColumnSet(true));

                    #region To Bill Details
                    if (SAF.Attributes.Contains("onl_contactpersonname2"))
                    {            //Billing information being sent to Child Account
                        childAccount["alletech_contactname"] = SAF.GetAttributeValue<String>("onl_contactpersonname2");
                        tracingService.Trace("alletech_contactfirstname : " + SAF.GetAttributeValue<String>("onl_contactpersonname2"));
                    }

                    if (SAF.Attributes.Contains("onl_emailid"))
                    {
                        childAccount["alletech_shippingemailid"] = SAF.GetAttributeValue<String>("onl_emailid");
                        tracingService.Trace("alletech_billingemailid : " + SAF.GetAttributeValue<String>("onl_emailid"));
                    }

                    if (SAF.Attributes.Contains("onl_phonenumberonl"))
                    {
                        childAccount["alletech_mobilephone2"] = SAF.GetAttributeValue<String>("onl_phonenumberonl");
                        tracingService.Trace("alletech_billingphoneno : " + SAF.GetAttributeValue<String>("onl_phonenumberonl"));
                    }
                    if (SAF.Attributes.Contains("onl_pincode"))
                    {
                        childAccount["alletech_bill_pincode"] = SAF.GetAttributeValue<String>("onl_pincode");
                        tracingService.Trace("alletech_pincodebill : " + SAF.GetAttributeValue<String>("onl_pincode"));
                    }
                    if (SAF.Attributes.Contains("onl_specifyareaonl"))
                    {
                        childAccount["alletech_bill_specifybillingarea"] = SAF.GetAttributeValue<String>("onl_specifyareaonl");
                        tracingService.Trace("onl_specifyareaonl : " + SAF.GetAttributeValue<String>("onl_specifyareaonl"));
                    }
                    if (SAF.Attributes.Contains("onl_specifybuildingonl"))
                    {
                        childAccount["alletech_bill_specifybuilding"] = SAF.GetAttributeValue<String>("onl_specifybuildingonl");
                        tracingService.Trace("alletech_specifybillingbuilding : " + SAF.GetAttributeValue<String>("onl_specifybuildingonl"));
                    }
                    if (SAF.Attributes.Contains("onl_spectra_block"))
                    {
                        childAccount["alletech_bill_buildingnoplotno"] = SAF.GetAttributeValue<String>("onl_spectra_block");
                        tracingService.Trace("alletech_blocktowernumberbill : " + SAF.GetAttributeValue<String>("onl_spectra_block"));
                    }
                    if (SAF.Attributes.Contains("onl_localityonl"))
                    {
                        childAccount["alletech_bill_locality"] = SAF.GetAttributeValue<String>("onl_localityonl");
                        tracingService.Trace("alletech_localitybill : " + SAF.GetAttributeValue<String>("onl_localityonl"));
                    }
                    if (SAF.Attributes.Contains("onl_floor"))
                    {
                        childAccount["alletech_bill_floor"] = SAF.GetAttributeValue<String>("onl_floor");
                        tracingService.Trace("alletech_floorbill : " + SAF.GetAttributeValue<String>("onl_floor"));
                    }
                    if (SAF.Attributes.Contains("onl_street"))
                    {
                        childAccount["alletech_bill_street"] = SAF.GetAttributeValue<String>("onl_street");
                        tracingService.Trace("alletech_streetbill : " + SAF.GetAttributeValue<String>("onl_street"));
                    }
                    if (SAF.Attributes.Contains("onl_landmarkifany"))
                    {
                        childAccount["alletech_bill_landmarkifany"] = SAF.GetAttributeValue<String>("onl_landmarkifany");
                        tracingService.Trace("alletech_landmarkbill : " + SAF.GetAttributeValue<String>("onl_landmarkifany"));
                    }
                    if (SAF.Attributes.Contains("onl_phonenumberonl"))
                    {
                        childAccount["alletech_bill_billingphoneno"] = SAF.GetAttributeValue<String>("onl_phonenumberonl");
                        tracingService.Trace("alletech_billingphoneno : " + SAF.GetAttributeValue<String>("onl_phonenumberonl"));
                    }

                    if (SAF.Attributes.Contains("onl_buildingtype"))
                    {
                        childAccount["alletech_bill_buildingtype"] = SAF.GetAttributeValue<OptionSetValue>("onl_buildingtype");
                        tracingService.Trace("alletech_buildingtypebill : " + SAF.FormattedValues["onl_buildingtype"].ToString());
                    }
                    if (SAF.Attributes.Contains("onl_areaonl"))
                    {
                        childAccount["alletech_bill_areamain"] = SAF.GetAttributeValue<EntityReference>("onl_areaonl");
                        tracingService.Trace("alletech_area1 : " + SAF.GetAttributeValue<EntityReference>("onl_areaonl").Name);
                        tracingService.Trace("alletech_area1 ID : " + SAF.GetAttributeValue<EntityReference>("onl_areaonl").Id);
                    }
                    if (SAF.Attributes.Contains("onl_cityonl"))
                    {
                        childAccount["alletech_bill_citymain"] = SAF.GetAttributeValue<EntityReference>("onl_cityonl");
                        tracingService.Trace("alletech_citybill : " + SAF.GetAttributeValue<EntityReference>("onl_cityonl").Name);
                        tracingService.Trace("alletech_citybill ID : " + SAF.GetAttributeValue<EntityReference>("onl_cityonl").Id);
                    }
                    if (SAF.Attributes.Contains("onl_countryonl"))
                    {
                        childAccount["alletech_bill_countrymain"] = SAF.GetAttributeValue<EntityReference>("onl_countryonl");
                        tracingService.Trace("alletech_countrybill : " + SAF.GetAttributeValue<EntityReference>("onl_countryonl").Name);
                        tracingService.Trace("alletech_countrybill ID : " + SAF.GetAttributeValue<EntityReference>("onl_countryonl").Id);
                    }
                    if (SAF.Attributes.Contains("onl_stateonl"))
                    {
                        childAccount["alletech_bill_statemain"] = SAF.GetAttributeValue<EntityReference>("onl_stateonl");
                        tracingService.Trace("alletech_statebill" + SAF.GetAttributeValue<EntityReference>("onl_stateonl").Name);
                        tracingService.Trace("alletech_statebill ID" + SAF.GetAttributeValue<EntityReference>("onl_stateonl").Id);
                    }
                    if (SAF.Attributes.Contains("onl_buildingnameonl"))
                    {
                        childAccount["alletech_bill_buildingname"] = SAF.GetAttributeValue<EntityReference>("onl_buildingnameonl");
                        tracingService.Trace("alletech_buildingnamebill : " + SAF.GetAttributeValue<EntityReference>("onl_buildingnameonl").Name);
                        tracingService.Trace("alletech_buildingnamebill ID : " + SAF.GetAttributeValue<EntityReference>("onl_buildingnameonl").Id);
                    }

                    #endregion

                    if (SAF.Contains("onl_panno"))
                    {
                        childAccount["pcl_panno"] = SAF.GetAttributeValue<string>("onl_panno");
                    }
                    if (SAF.Contains("onl_tanno") && SAF["onl_tanno"] != null)
                    {
                        childAccount["pcl_tanno"] = SAF.GetAttributeValue<string>("onl_tanno");
                    }

                    Entity OppEntityall = service.Retrieve("opportunity", Oppid, new ColumnSet(true));
                    if (SAF.Contains("onl_productonl"))
                        childAccount["alletech_product"] = SAF.GetAttributeValue<EntityReference>("onl_productonl");
                    if (SAF.Contains("onl_websiteurl"))
                        childAccount["websiteurl"] = OppEntityall.GetAttributeValue<String>("onl_websiteurl");
                    if (SAF.Contains("onl_industrytypeonl"))
                        childAccount["alletech_industry"] = OppEntityall.GetAttributeValue<String>("onl_industrytypeonl");
                    if (SAF.Contains("onl_firmtypeonl"))
                        childAccount["alletech_firmtype"] = OppEntityall.GetAttributeValue<String>("onl_firmtypeonl");

                    #region To Ship Details
                    QueryExpression querycustomersite = new QueryExpression();
                    querycustomersite.EntityName = "onl_customersite";
                    querycustomersite.ColumnSet = new ColumnSet(true);
                    querycustomersite.Criteria.AddCondition("onl_opportunityidid", ConditionOperator.Equal, Oppid);
                    querycustomersite.Criteria.AddCondition("onl_sitetype", ConditionOperator.Equal, "122050000");//Hub Condition
                    EntityCollection resultcustomersite = service.RetrieveMultiple(querycustomersite);

                    int rownumber = 1;
                    foreach (Entity entity in resultcustomersite.Entities)
                    {
                        if(rownumber==1)
                        {
                            
                            if (entity.Attributes.Contains("spectra_pincode"))
                            {
                                childAccount["alletech_zippostalcode"] = entity.GetAttributeValue<String>("spectra_pincode");
                                tracingService.Trace("alletech_zippostalcode : " + entity.GetAttributeValue<String>("spectra_pincode"));
                            }
                            if (entity.Attributes.Contains("onl_address"))
                            {
                                childAccount["alletech_street"] = entity.GetAttributeValue<String>("onl_address");
                                tracingService.Trace("alletech_street : " + SAF.GetAttributeValue<String>("onl_address"));
                            }
                            if (entity.Attributes.Contains("onl_state"))
                            {
                                childAccount["alletech_state"] = entity.GetAttributeValue<EntityReference>("onl_state");
                                tracingService.Trace("alletech_ftthstate : " + entity.GetAttributeValue<EntityReference>("onl_state").Name);
                                tracingService.Trace("alletech_ftthstate ID : " + entity.GetAttributeValue<EntityReference>("onl_state").Id);
                            }

                            if (entity.Attributes.Contains("onl_city"))
                            {
                                childAccount["alletech_city"] = entity.GetAttributeValue<EntityReference>("onl_city");
                                tracingService.Trace("alletech_ftthcity : " + entity.GetAttributeValue<EntityReference>("onl_city").Name);
                                tracingService.Trace("alletech_ftthcity ID : " + entity.GetAttributeValue<EntityReference>("onl_city").Id);
                            }

                        }
                    }
                       

                      
                   

                    #endregion

                    tracingService.Trace("before updating");
                  

                    service.Update(childAccount);

                    tracingService.Trace("Child account update completed");
                }
                else
                {
                    throw new InvalidPluginExecutionException("Account Record Not Updated");
                }
                //}
            }
            catch (Exception e)
            {
                throw new InvalidPluginExecutionException("Account Record not updated-" + e.Message);
            }
        }
        public void AccountShortName2(IOrganizationService service, Entity CAF, string canId, IPluginExecutionContext context)
        {

            if (context.Depth == 1)
            {
                ConditionExpression condition2 = new ConditionExpression("alletech_accountid", ConditionOperator.Equal, canId);
                FilterExpression filter2 = new FilterExpression();
                filter2.AddCondition(condition2);
                filter2.FilterOperator = LogicalOperator.And;
                QueryExpression query2 = new QueryExpression
                {
                    EntityName = "account",
                    ColumnSet = new ColumnSet("alletech_unifyshortname"),
                    Criteria = filter2,
                };
                EntityCollection childAccountCollection = service.RetrieveMultiple(query2);
                if (childAccountCollection.Entities.Count == 0)
                    return;
                Entity childAccount = childAccountCollection.Entities[0];

                tracingService.Trace("child account = " + childAccountCollection.Entities[0]);
                tracingService.Trace("if (CAF.Attributes.Contains('alletech_parentaccount')) = " + (CAF.Attributes.Contains("alletech_parentaccount")));

                if (CAF.Attributes.Contains("onl_parentaccountonl"))
                {
                    Guid paraccid = CAF.GetAttributeValue<EntityReference>("onl_parentaccountonl").Id;
                    ConditionExpression condition = new ConditionExpression("accountid", ConditionOperator.Equal, paraccid);
                    FilterExpression filter = new FilterExpression();
                    filter.AddCondition(condition);
                    filter.FilterOperator = LogicalOperator.And;
                    QueryExpression query = new QueryExpression
                    {
                        EntityName = "account",
                        ColumnSet = new ColumnSet("alletech_unifyshortname"),
                        Criteria = filter,
                    };

                    EntityCollection parentaccountcollection = service.RetrieveMultiple(query);
                    if (parentaccountcollection.Entities.Count == 0)
                        return;
                    Entity ChildParentAccount = parentaccountcollection.Entities[0];

                    #region if parent account contains short name
                    if (ChildParentAccount.Contains("alletech_unifyshortname"))
                    {
                        string paraccshrt = ChildParentAccount.GetAttributeValue<String>("alletech_unifyshortname");
                        // string pashrt = paraccshrt.Substring(0, 4);
                        string pashrt = paraccshrt.ToString();

                        ConditionExpression condition4 = new ConditionExpression("parentaccountid", ConditionOperator.Equal, ChildParentAccount.Id);
                        //ConditionExpression condition5 = new ConditionExpression("alletech_unifyshortname", ConditionOperator.BeginsWith, pashrt);
                        FilterExpression filter4 = new FilterExpression();
                        filter4.FilterOperator = LogicalOperator.And;
                        filter4.AddCondition(condition4);
                        //filter4.AddCondition(condition5);
                        QueryExpression query4 = new QueryExpression
                        {
                            EntityName = "account",
                            ColumnSet = new ColumnSet("alletech_unifyshortname"),
                            Criteria = filter4,
                        };
                        query4.AddOrder("alletech_unifyshortname", OrderType.Descending);
                        EntityCollection chldacc = service.RetrieveMultiple(query4);
                        string cldsrt = String.Empty;
                        if (chldacc.Entities.Count == 0)
                        {
                            cldsrt = "01";
                        }
                        else
                        {
                            if (chldacc.Entities[0].Contains("alletech_unifyshortname"))
                            {
                                string chldshrt = chldacc.Entities[0].GetAttributeValue<string>("alletech_unifyshortname");

                                if (PRDSeg == "MAC")
                                    cldsrt = "01-P";
                                else
                                {
                                    #region child short name count
                                    cldsrt = chldshrt.Split('-')[1];
                                    int cldsrtno = Convert.ToInt32(cldsrt);
                                    cldsrtno++;
                                    if (cldsrtno >= 1 && cldsrtno < 10)
                                    {
                                        cldsrt = "0" + cldsrtno.ToString();
                                    }
                                    else
                                    {
                                        cldsrt = cldsrtno.ToString();
                                    }
                                    #endregion
                                }
                            }
                            else
                            {
                                cldsrt = "01";
                            }
                        }
                        string finalcldsrt = pashrt + "-" + cldsrt;
                        childAccount.Attributes["alletech_unifyshortname"] = finalcldsrt;

                        service.Update(childAccount);

                    }
                    #endregion

                    #region parent account accoutn doesnt contains short name

                    else
                    {
                        ConditionExpression condition01 = new ConditionExpression("alletech_name", ConditionOperator.Equal, "SDAccount_Short");
                        FilterExpression filter01 = new FilterExpression();
                        filter01.AddCondition(condition01);
                        filter01.FilterOperator = LogicalOperator.And;
                        QueryExpression query3 = new QueryExpression
                        {
                            EntityName = "alletech_configuration",
                            ColumnSet = new ColumnSet(true),
                            Criteria = filter01,
                        };
                        EntityCollection accconfig = service.RetrieveMultiple(query3);
                        string counterstr = String.Empty;
                        int counter = accconfig.Entities[0].GetAttributeValue<int>("alletech_autoidcounter");
                        counter++;


                        int length = counter.ToString().Length;
                        if (length < 4)
                        {
                            for (int i = 0; i < (4 - length); i++)
                                counterstr = counterstr + "0";
                        }
                        counterstr = counterstr + counter.ToString();

                        service.Update(accconfig.Entities[0]);
                        ChildParentAccount.Attributes["alletech_unifyshortname"] = counterstr;
                        service.Update(ChildParentAccount);

                        //ConditionExpression condition02 = new ConditionExpression("alletech_accountid", ConditionOperator.Equal, canId);
                        //FilterExpression filter02 = new FilterExpression();
                        //filter02.AddCondition(condition02);
                        //filter02.FilterOperator = LogicalOperator.And;
                        //QueryExpression query02 = new QueryExpression
                        //{
                        //    EntityName = "account",
                        //    ColumnSet = new ColumnSet("alletech_unifyshortname"),
                        //    Criteria = filter02,
                        //};
                        //EntityCollection childAccountCollection2 = service.RetrieveMultiple(query02);
                        //if (childAccountCollection2.Entities.Count == 0)
                        //    return;

                        //Entity childAccount2 = childAccountCollection2.Entities[0];
                        //childAccount2.Attributes["alletech_unifyshortname"] = counterstr;

                        //service.Update(childAccount2);


                    }
                    #endregion
                }
            }
        }
        public void AccountShortName1(IOrganizationService service, Entity SAF, IPluginExecutionContext context, string canId)
        {


            if (context.Depth == 1)
            {
                if (!SAF.Attributes.Contains("onl_parentaccountonl"))
                {

                    ConditionExpression condition = new ConditionExpression("alletech_accountid", ConditionOperator.Equal, canId);
                    FilterExpression filter = new FilterExpression();
                    filter.AddCondition(condition);
                    filter.FilterOperator = LogicalOperator.And;
                    QueryExpression query = new QueryExpression
                    {
                        EntityName = "account",
                        ColumnSet = new ColumnSet("parentaccountid", "alletech_unifyshortname"),
                        Criteria = filter,
                    };
                    EntityCollection childAccountCollection = service.RetrieveMultiple(query);
                    if (childAccountCollection.Entities.Count == 0)
                        return;
                    Entity childAccount = childAccountCollection.Entities[0];

                    if (!childAccount.Contains("alletech_unifyshortname"))
                    {
                        string count = String.Empty;
                        string prefix = String.Empty;
                        if (PRDSeg == "BBB")
                        {
                            count = "H" + canId;
                        }
                        else
                        {
                            ConditionExpression condition01 = new ConditionExpression("alletech_name", ConditionOperator.Equal, "SDAccount_Short");
                            FilterExpression filter01 = new FilterExpression();
                            filter01.AddCondition(condition01);
                            filter01.FilterOperator = LogicalOperator.And;
                            QueryExpression query2 = new QueryExpression
                            {
                                EntityName = "alletech_configuration",
                                ColumnSet = new ColumnSet("alletech_autoidcounter", "alletech_autoidprefix"),
                                Criteria = filter01,
                            };
                            EntityCollection accconfig = service.RetrieveMultiple(query2);
                            if (accconfig.Entities.Count == 0)
                                return;
                            Entity accconf = accconfig.Entities[0];

                            if (accconf.Contains("alletech_autoidcounter"))
                            {
                                int counter = (int)accconf.GetAttributeValue<int>("alletech_autoidcounter");
                                counter++;
                                accconf.Attributes["alletech_autoidcounter"] = counter;
                                service.Update(accconf);

                                int length = counter.ToString().Length;
                                if (length < 4)
                                {
                                    for (int i = 0; i < (4 - length); i++)
                                        count = count + "0";
                                }
                                count = count + counter.ToString();
                                prefix = accconf.GetAttributeValue<string>("alletech_autoidprefix");
                            }
                        }
                        //var parentaccid = SAF.GetAttributeValue<EntityReference>("parentaccountid").Id;
                        //  Entity parentaccnt = service.Retrieve("account", parentaccid, new ColumnSet("accountid"));
                        //  parentaccnt.Attributes["alletech_unifyshortname"] = count;
                        //                        service.Update(parentaccnt);


                        string finalcount = count;

                        string Accountno = GetAccountNo(service, CreateAccountId);

                        //Get Parent Account -Account No 
                        #region Parent Account update Shortname
                        ConditionExpression conditionParentAcc = new ConditionExpression("alletech_accountid", ConditionOperator.Equal, Accountno);
                        FilterExpression filterParent = new FilterExpression();
                        filterParent.AddCondition(conditionParentAcc);
                        filterParent.FilterOperator = LogicalOperator.And;
                        QueryExpression queryParent = new QueryExpression
                        {
                            EntityName = "account",
                            ColumnSet = new ColumnSet("alletech_unifyshortname"),
                            Criteria = filter,
                        };
                        EntityCollection ParentAccountCollection = service.RetrieveMultiple(queryParent);
                        if (ParentAccountCollection.Entities.Count == 0)
                            return;
                        Entity ChildParentAccount = ParentAccountCollection.Entities[0];
                        ChildParentAccount.Attributes["alletech_unifyshortname"] = prefix + finalcount;

                        service.Update(ChildParentAccount);
                        #endregion

                        #region Existing Account Update Shortname with -01
                        childAccount.Attributes["alletech_unifyshortname"] = prefix + finalcount + "-01";
                        service.Update(childAccount);
                        #endregion

                    }
                    else
                    {

                        string parentaccshort = childAccount.GetAttributeValue<string>("alletech_unifyshortname");
                        string paccshort = parentaccshort.Split('-')[0];
                        Entity pacc = service.Retrieve("account", childAccount.GetAttributeValue<EntityReference>("parentaccountid").Id, new ColumnSet("accountid"));
                        pacc.Attributes["alletech_unifyshortname"] = paccshort;

                        service.Update(pacc);

                    }
                }
            }
        }
        private void ReadUnSecuredConfig(string localConfig)
        {
            string key = string.Empty;
            try
            {
                this.globalConfig = new Dictionary<string, string>();
                if (!string.IsNullOrEmpty(localConfig))
                {
                    XmlDocument doc = new XmlDocument();

                    doc.LoadXml(localConfig);

                    foreach (XmlElement entityNode in doc.SelectNodes("/appSettings/add"))
                    {
                        key = entityNode.GetAttribute("key").ToString();
                        this.globalConfig.Add(entityNode.GetAttribute("key").ToString(), entityNode.GetAttribute("value").ToString());
                    }
                }
            }
            catch (FaultException<OrganizationServiceFault> ex)
            {
                throw ex;
            }
            catch (Exception e)
            {
                throw e;
            }
        }
        private string GetValueForKey(string keyName)
        {
            string valueString = string.Empty;
            try
            {

                if (this.globalConfig.ContainsKey(keyName))
                {
                    valueString = this.globalConfig[keyName];
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return valueString;
        }
        public EntityReference retrieveDomain(Guid city, IOrganizationService service, Guid Productsegmentid)
        {
            EntityReference childAccountDomain = null;
            try
            {
                QueryExpression queryConfig = new QueryExpression("alletech_domain");
                queryConfig.ColumnSet = new ColumnSet("alletech_domainid");
                queryConfig.Criteria.AddCondition("pcl_city", ConditionOperator.Equal, city);
                queryConfig.Criteria.AddCondition("spectra_productsegment", ConditionOperator.Equal, Productsegmentid);

                EntityCollection domainCollection = service.RetrieveMultiple(queryConfig);
                if (domainCollection.Entities.Count > 0)
                {
                    tracingService.Trace("Domain record found");
                    childAccountDomain = new EntityReference("alletech_domain", domainCollection.Entities[0].Id);
                }
                else
                    tracingService.Trace("no domain records");
            }
            catch (Exception e)
            {
                tracingService.Trace("UnifyIntegration.IntegrationSAF.retrieveDomain() : Domain Retrieval Failed.." + e);
            }
            return childAccountDomain;
        }
        public string DateFormater(DateTime d)
        {
            string day = d.Day.ToString();
            string month = d.Month.ToString();
            string year = d.Year.ToString();

            if (day.Length == 1)
                day = "0" + day;
            if (month.Length == 1)
                month = "0" + month;

            string date = year + "-" + month + "-" + day;
            string ISOformattedDate = date + "T00:00:00";//+05:30";
            return ISOformattedDate;

        }
        public Entity GetResultByAttribute(IOrganizationService _service, string entityName, string attrName, string attrValue, string column)
        {
            try
            {

                Entity result = null;
                QueryExpression query = new QueryExpression(entityName);


                query.NoLock = true;

                if (column == "all")
                    query.ColumnSet.AllColumns = true;
                else
                    query.ColumnSet.AddColumns(column);

                query.Criteria.AddCondition(attrName, ConditionOperator.Equal, attrValue);

                EntityCollection resultcollection = _service.RetrieveMultiple(query);

                if (resultcollection.Entities.Count > 0)
                    return resultcollection.Entities[0];
                else
                    return result;

            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException(ex.Message);
            }
        }
    }
}
