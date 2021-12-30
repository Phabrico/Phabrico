using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Phabrico.Miscellaneous;
using System.Linq;
using static Phabrico.Controllers.Synchronization;

namespace Phabrico.UnitTests.Synchronization
{
    [TestClass]
    public class UploadPhrictionBulkTest : PhabricoUnitTest
    {
        Phabrico.Controllers.Synchronization synchronizationController;
        DummyPhabricatorWebServer phabricatorWebServer;

        protected override void Initialize(string httpRootPath)
        {
            base.Initialize(httpRootPath);

            Miscellaneous.HttpListenerContext httpListenerContext = new Miscellaneous.HttpListenerContext();

            synchronizationController = new Phabrico.Controllers.Synchronization();
            synchronizationController.browser = new Http.Browser(HttpServer, httpListenerContext);
            synchronizationController.EncryptionKey = EncryptionKey;
            synchronizationController.TokenId = HttpServer.Session.ClientSessions.LastOrDefault().Key;
            
            Http.SessionManager.Token token = synchronizationController.browser.HttpServer.Session.CreateToken(EncryptionKey, synchronizationController.browser);
            synchronizationController.browser.SetCookie("token", token.ID, true);
            token.PrivateEncryptionKey = EncryptionKey;

            phabricatorWebServer = new DummyPhabricatorWebServer();
        }

        public override void Dispose()
        {
            phabricatorWebServer.Stop();

            base.Dispose();
        }

        [TestMethod]
        [DataRow("")]
        [DataRow("phabrico")]
        public void TestPhrictionBulkTest(string rootPath)
        {
            Initialize(rootPath);

            string[] stageInfoRecords = new string[] {
                @"{""Author"":null,""Content"":"" "",""DateModified"":""2021-12-07T08:39:30.0151227+00:00"",""DisplayOrderInFavorites"":0,""LastModifiedBy"":null,""Name"":""A"",""Path"":""singalong/"",""Projects"":"""",""Subscribers"":"""",""Token"":""PHID-NEWTOKEN-0000000000000501"",""TokenPrefix"":""PHID-WIKI-"",""Language"":{}}",
                @"{""Author"":null,""Content"":"" "",""DateModified"":""2021-12-07T08:39:31.0151227+00:00"",""DisplayOrderInFavorites"":0,""LastModifiedBy"":null,""Name"":""B"",""Path"":""singalong/oobiedoobilies/"",""Projects"":"""",""Subscribers"":"""",""Token"":""PHID-NEWTOKEN-0000000000000502"",""TokenPrefix"":""PHID-WIKI-"",""Language"":{}}",
                @"{""Author"":null,""Content"":"" "",""DateModified"":""2021-12-07T08:39:32.0151227+00:00"",""DisplayOrderInFavorites"":0,""LastModifiedBy"":null,""Name"":""C"",""Path"":""singalong/oobiedoobilies/coffeebluesies/"",""Projects"":"""",""Subscribers"":"""",""Token"":""PHID-NEWTOKEN-0000000000000503"",""TokenPrefix"":""PHID-WIKI-"",""Language"":{}}",
                @"{""Author"":null,""Content"":"" "",""DateModified"":""2021-12-07T08:39:33.0151227+00:00"",""DisplayOrderInFavorites"":0,""LastModifiedBy"":null,""Name"":""D"",""Path"":""singalong/oobiedoobilies/coffeebluesies/rck/"",""Projects"":"""",""Subscribers"":"""",""Token"":""PHID-NEWTOKEN-0000000000000504"",""TokenPrefix"":""PHID-WIKI-"",""Language"":{}}",
                @"{""Author"":null,""Content"":"" "",""DateModified"":""2021-12-07T08:39:34.0151227+00:00"",""DisplayOrderInFavorites"":0,""LastModifiedBy"":null,""Name"":""E"",""Path"":""singalong/oobiedoobilies/coffeebluesies/rck/indyrockish/"",""Projects"":"""",""Subscribers"":"""",""Token"":""PHID-NEWTOKEN-0000000000000505"",""TokenPrefix"":""PHID-WIKI-"",""Language"":{}}",
                @"{""Author"":null,""Content"":""In München steht ein hofbräuhaus"",""DateModified"":""2021-12-07T09:00:55.579844+00:00"",""DisplayOrderInFavorites"":0,""LastModifiedBy"":null,""Name"":""F"",""Path"":""singalong/oobiedoobilies/coffeebluesies/rck/indyrockish/aaaaaaaaaaaa/"",""Projects"":"""",""Subscribers"":"""",""Token"":""PHID-NEWTOKEN-0000000000000010"",""TokenPrefix"":""PHID-WIKI-"",""Language"":{}}",
                @"{""Author"":null,""Content"":"" "",""DateModified"":""2021-12-07T09:05:35.6451724+00:00"",""DisplayOrderInFavorites"":0,""LastModifiedBy"":null,""Name"":""G"",""Path"":""singalong/oobiedoobilies/coffeebluesies/rck/indyrockish/bbbbbbb/"",""Projects"":"""",""Subscribers"":"""",""Token"":""PHID-NEWTOKEN-0000000000000016"",""TokenPrefix"":""PHID-WIKI-"",""Language"":{}}",
                @"{""Author"":null,""Content"":"" "",""DateModified"":""2021-12-07T09:11:52.653054+00:00"",""DisplayOrderInFavorites"":0,""LastModifiedBy"":null,""Name"":""H"",""Path"":""singalong/oobiedoobilies/coffeebluesies/rck/indyrockish/bbbbbbb/instrumentals/"",""Projects"":"""",""Subscribers"":"""",""Token"":""PHID-NEWTOKEN-0000000000000017"",""TokenPrefix"":""PHID-WIKI-"",""Language"":{}}",
                @"{""Author"":null,""Content"":""Cucarachas enojadas fumando marihuana "",""DateModified"":""2021-12-07T14:30:01.4521051+00:00"",""DisplayOrderInFavorites"":0,""LastModifiedBy"":null,""Name"":""I"",""Path"":""singalong/oobiedoobilies/coffeebluesies/rck/indyrockish/bbbbbbb/instrumentals/c-guitar/aaaaaaaaaaaa_bbbbbb_-_ccccccc_ddddd/"",""Projects"":"""",""Subscribers"":"""",""Token"":""PHID-NEWTOKEN-0000000000000024"",""TokenPrefix"":""PHID-WIKI-"",""Language"":{}}",
                @"{""Author"":null,""Content"":""J'attendrai le jour et la nuit"",""DateModified"":""2021-12-08T08:05:54.2112526+00:00"",""DisplayOrderInFavorites"":0,""LastModifiedBy"":null,""Name"":""J"",""Path"":""singalong/oobiedoobilies/coffeebluesies/rck/indyrockish/bbbbbbb/instrumentals/c-guitar/bbbbb_cccccccc_-_ddddddd/"",""Projects"":"""",""Subscribers"":"""",""Token"":""PHID-NEWTOKEN-0000000000000020"",""TokenPrefix"":""PHID-WIKI-"",""Language"":{}}",
                @"{""Author"":null,""Content"":""Bland skuggor rider en odjur"",""DateModified"":""2021-12-08T08:32:02.4801665+00:00"",""DisplayOrderInFavorites"":0,""LastModifiedBy"":null,""Name"":""K"",""Path"":""singalong/oobiedoobilies/coffeebluesies/rck/indyrockish/bbbbbbb/instrumentals/c-guitar/aaaaaaaaaaaa_bbbbbb_-_eeeeeeeee_fffff/"",""Projects"":"""",""Subscribers"":"""",""Token"":""PHID-NEWTOKEN-0000000000000069"",""TokenPrefix"":""PHID-WIKI-"",""Language"":{}}",
                @"{""Author"":null,""Content"":""Wie kant wat schelen, zo zijn er zovelen"",""DateModified"":""2021-12-08T14:21:10.8594882+00:00"",""DisplayOrderInFavorites"":0,""LastModifiedBy"":null,""Name"":""L"",""Path"":""singalong/oobiedoobilies/coffeebluesies/rck/indyrockish/bbbbbbb/instrumentals/c-guitar/aaaaaaaaaaaa_bbbbbb_-_ggggg/"",""Projects"":"""",""Subscribers"":"""",""Token"":""PHID-NEWTOKEN-0000000000000071"",""TokenPrefix"":""PHID-WIKI-"",""Language"":{}}",
                @"{""Author"":null,""Content"":""Mørket kommer listende og nu må jeg stå for skud"",""DateModified"":""2021-12-08T15:08:19.6202552+00:00"",""DisplayOrderInFavorites"":0,""LastModifiedBy"":null,""Name"":""M"",""Path"":""singalong/oobiedoobilies/coffeebluesies/rck/indyrockish/bbbbbbb/instrumentals/c-guitar/aaaaaaaaaaaa_bbbbbb_-_hhhhhh-iiiiiiii_jjjjjjj/"",""Projects"":"""",""Subscribers"":"""",""Token"":""PHID-NEWTOKEN-0000000000000089"",""TokenPrefix"":""PHID-WIKI-"",""Language"":{}}",
                @"{""Author"":null,""Content"":""We believe in freedom, we believe in human rights"",""DateModified"":""2021-12-08T15:45:46.29152+00:00"",""DisplayOrderInFavorites"":0,""LastModifiedBy"":null,""Name"":""N"",""Path"":""singalong/oobiedoobilies/coffeebluesies/rck/indyrockish/bbbbbbb/instrumentals/c-guitar/ccccccc_-_dddd_eeeeeeeeee/"",""Projects"":"""",""Subscribers"":"""",""Token"":""PHID-NEWTOKEN-0000000000000091"",""TokenPrefix"":""PHID-WIKI-"",""Language"":{}}",
                @"{""Author"":null,""Content"":""Ridin' down the highway"",""DateModified"":""2021-12-09T08:20:48.4322817+00:00"",""DisplayOrderInFavorites"":0,""LastModifiedBy"":null,""Name"":""O"",""Path"":""singalong/oobiedoobilies/coffeebluesies/rck/indyrockish/bbbbbbb/instrumentals/c-guitar/aaaaaaaaaaaa_bbbbbb_-_kkkkkkk_llllll/"",""Projects"":"""",""Subscribers"":"""",""Token"":""PHID-NEWTOKEN-0000000000000041"",""TokenPrefix"":""PHID-WIKI-"",""Language"":{}}",
                @"{""Author"":null,""Content"":""Prenez un enfant et faites-en un roi"",""DateModified"":""2021-12-09T14:58:46.8008367+00:00"",""DisplayOrderInFavorites"":0,""LastModifiedBy"":null,""Name"":""P"",""Path"":""singalong/oobiedoobilies/coffeebluesies/rck/indyrockish/bbbbbbb/instrumentals/c-guitar/ddddddd_eeeeeeeeeeee_-_ffffffffffff_ggggggg/"",""Projects"":"""",""Subscribers"":"""",""Token"":""PHID-NEWTOKEN-0000000000000122"",""TokenPrefix"":""PHID-WIKI-"",""Language"":{}}",
                @"{""Author"":null,""Content"":""Streichholz und Benzinkanister"",""DateModified"":""2021-12-09T15:14:03.2360165+00:00"",""DisplayOrderInFavorites"":0,""LastModifiedBy"":null,""Name"":""Q"",""Path"":""singalong/oobiedoobilies/coffeebluesies/rck/indyrockish/femalevocal/"",""Projects"":"""",""Subscribers"":"""",""Token"":""PHID-NEWTOKEN-0000000000000039"",""TokenPrefix"":""PHID-WIKI-"",""Language"":{}}",
                @"{""Author"":null,""Content"":""Katseet ylös luokaa veljet, aika tullut on,"",""DateModified"":""2021-12-09T15:32:54.9184257+00:00"",""DisplayOrderInFavorites"":0,""LastModifiedBy"":null,""Name"":""R"",""Path"":""singalong/oobiedoobilies/coffeebluesies/rck/indyrockish/bbbbbbb/instrumentals/c-guitar/ddddddd_eeeeeeeeeeee_-_hhhhhh_iiiiiiiiiiii/"",""Projects"":"""",""Subscribers"":"""",""Token"":""PHID-NEWTOKEN-0000000000000128"",""TokenPrefix"":""PHID-WIKI-"",""Language"":{}}",
                @"{""Author"":null,""Content"":""Det e' ondskapen som her rår."",""DateModified"":""2021-12-09T15:47:28.3146251+00:00"",""DisplayOrderInFavorites"":0,""LastModifiedBy"":null,""Name"":""S"",""Path"":""singalong/oobiedoobilies/coffeebluesies/rck/indyrockish/bbbbbbb/instrumentals/c-guitar/eeeeee_ffffffffff_-_ggg_hhhhhhh/"",""Projects"":"""",""Subscribers"":"""",""Token"":""PHID-NEWTOKEN-0000000000000130"",""TokenPrefix"":""PHID-WIKI-"",""Language"":{}}",
                @"{""Author"":null,""Content"":""Niemand bracht mij ooit van mijn stuk"",""DateModified"":""2021-12-09T15:53:49.7171554+00:00"",""DisplayOrderInFavorites"":0,""LastModifiedBy"":null,""Name"":""T"",""Path"":""singalong/oobiedoobilies/coffeebluesies/rck/indyrockish/bbbbbbb/instrumentals/c-guitar/eeeeee_ffffffffff_-_iiiii/"",""Projects"":"""",""Subscribers"":"""",""Token"":""PHID-NEWTOKEN-0000000000000132"",""TokenPrefix"":""PHID-WIKI-"",""Language"":{}}",
                @"{""Author"":null,""Content"":""There's colors on the street"",""DateModified"":""2021-12-09T15:57:24.6285752+00:00"",""DisplayOrderInFavorites"":0,""LastModifiedBy"":null,""Name"":""U"",""Path"":""singalong/oobiedoobilies/coffeebluesies/rck/indyrockish/bbbbbbb/instrumentals/c-guitar/"",""Projects"":"""",""Subscribers"":"""",""Token"":""PHID-NEWTOKEN-0000000000000018"",""TokenPrefix"":""PHID-WIKI-"",""Language"":{}}",
                @"{""Author"":null,""Content"":""Den byder til alle sin sælsomme Gunst"",""DateModified"":""2021-12-10T06:12:34.4602471+00:00"",""DisplayOrderInFavorites"":0,""LastModifiedBy"":null,""Name"":""V"",""Path"":""singalong/oobiedoobilies/coffeebluesies/rck/indyrockish/bbbbbbb/instrumentals/c-guitar/eeeeee_ffffffffff_-_jjj/"",""Projects"":"""",""Subscribers"":"""",""Token"":""PHID-NEWTOKEN-0000000000000136"",""TokenPrefix"":""PHID-WIKI-"",""Language"":{}}",
                @"{""Author"":null,""Content"":""Min barndoms träd stå höga i gräset"",""DateModified"":""2021-12-10T07:24:43.8996542+00:00"",""DisplayOrderInFavorites"":0,""LastModifiedBy"":null,""Name"":""W"",""Path"":""singalong/oobiedoobilies/coffeebluesies/rck/indyrockish/bbbbbbb/instrumentals/c-guitar/eeeeee_ffffffffff_-_kkk/kkk_llllll/"",""Projects"":"""",""Subscribers"":"""",""Token"":""PHID-NEWTOKEN-0000000000000154"",""TokenPrefix"":""PHID-WIKI-"",""Language"":{}}",
                @"{""Author"":null,""Content"":""Nesta noite branca, sou um boneco de neve"",""DateModified"":""2021-12-10T09:30:59.313653+00:00"",""DisplayOrderInFavorites"":0,""LastModifiedBy"":null,""Name"":""X"",""Path"":""singalong/oobiedoobilies/coffeebluesies/rck/indyrockish/bbbbbbb/instrumentals/c-guitar/eeeeee_ffffffffff_-_kkk/"",""Projects"":"""",""Subscribers"":"""",""Token"":""PHID-NEWTOKEN-0000000000000151"",""TokenPrefix"":""PHID-WIKI-"",""Language"":{}}",
                @"{""Author"":null,""Content"":""Ella tenía sólo diecisiete años"",""DateModified"":""2021-12-10T09:32:22.7729816+00:00"",""DisplayOrderInFavorites"":0,""LastModifiedBy"":null,""Name"":""Y"",""Path"":""singalong/oobiedoobilies/coffeebluesies/rck/indyrockish/bbbbbbb/instrumentals/c-guitar/ccccccc_-_fffffffff/"",""Projects"":"""",""Subscribers"":"""",""Token"":""PHID-NEWTOKEN-0000000000000099"",""TokenPrefix"":""PHID-WIKI-"",""Language"":{}}",
                @"{""Author"":null,""Content"":""Che non mi si chiudeva più lo stomaco"",""DateModified"":""2021-12-10T10:43:58.641066+00:00"",""DisplayOrderInFavorites"":0,""LastModifiedBy"":null,""Name"":""Z"",""Path"":""singalong/oobiedoobilies/coffeebluesies/rck/indyrockish/bbbbbbb/instrumentals/c-guitar/hhhhhhh_iiiiiiiiii/"",""Projects"":"""",""Subscribers"":"""",""Token"":""PHID-NEWTOKEN-0000000000000170"",""TokenPrefix"":""PHID-WIKI-"",""Language"":{}}",
                @"{""Author"":null,""Content"":""Si një qielli ngrysur, duken sytë e tu"",""DateModified"":""2021-12-10T12:46:51.2813641+00:00"",""DisplayOrderInFavorites"":0,""LastModifiedBy"":null,""Name"":""A1"",""Path"":""singalong/oobiedoobilies/coffeebluesies/rck/indyrockish/bbbbbbb/instrumentals/c-guitar/"",""Projects"":"""",""Subscribers"":"""",""Token"":""PHID-NEWTOKEN-0000000000000174"",""TokenPrefix"":""PHID-WIKI-"",""Language"":{}}",
                @"{""Author"":null,""Content"":"""",""DateModified"":""2021-12-10T12:48:08.1432727+00:00"",""DisplayOrderInFavorites"":0,""LastModifiedBy"":null,""Name"":""B1"",""Path"":""singalong/oobiedoobilies/coffeebluesies/rck/indyrockish/bbbbbbb/drums/"",""Projects"":"""",""Subscribers"":"""",""Token"":""PHID-NEWTOKEN-0000000000000189"",""TokenPrefix"":""PHID-WIKI-"",""Language"":{}}",
                @"{""Author"":null,""Content"":""Oi tregueregue oi detero deguedo"",""DateModified"":""2021-12-10T12:53:55.7831762+00:00"",""DisplayOrderInFavorites"":0,""LastModifiedBy"":null,""Name"":""C1"",""Path"":""singalong/oobiedoobilies/coffeebluesies/rck/indyrockish/bbbbbbb/drums/aaaaaa_bbbbbbbb/"",""Projects"":"""",""Subscribers"":"""",""Token"":""PHID-NEWTOKEN-0000000000000191"",""TokenPrefix"":""PHID-WIKI-"",""Language"":{}}",
                @"{""Author"":null,""Content"":""Wanna tell you a story"",""DateModified"":""2021-12-10T13:10:09.8552602+00:00"",""DisplayOrderInFavorites"":0,""LastModifiedBy"":null,""Name"":""D1"",""Path"":""singalong/oobiedoobilies/coffeebluesies/rck/indyrockish/bbbbbbb/drums/cccccc_dddddd_eeeeeeeeee/"",""Projects"":"""",""Subscribers"":"""",""Token"":""PHID-NEWTOKEN-0000000000000193"",""TokenPrefix"":""PHID-WIKI-"",""Language"":{}}",
                @"{""Author"":null,""Content"":""Noir c'est noir"",""DateModified"":""2021-12-10T15:52:36.2370749+00:00"",""DisplayOrderInFavorites"":0,""LastModifiedBy"":null,""Name"":""E1"",""Path"":""singalong/oobiedoobilies/coffeebluesies/rck/indyrockish/bbbbbbb/drums/ffffffff_ggggggggg/"",""Projects"":"""",""Subscribers"":"""",""Token"":""PHID-NEWTOKEN-0000000000000195"",""TokenPrefix"":""PHID-WIKI-"",""Language"":{}}"
            };

            // prepare test
            Miscellaneous.HttpListenerContext httpListenerContext = new Miscellaneous.HttpListenerContext();
            SynchronizationParameters synchronizationParameters = new SynchronizationParameters();
            synchronizationParameters.browser = new Http.Browser(HttpServer, httpListenerContext);
            synchronizationParameters.browser.Conduit = new Phabricator.API.Conduit(HttpServer, synchronizationParameters.browser);
            synchronizationParameters.browser.Conduit.PhabricatorUrl = "http://127.0.0.2:46975";
            synchronizationParameters.database = Database;
            synchronizationParameters.existingAccount = accountWhoAmI;
            synchronizationParameters.projectSelected[Phabricator.Data.Project.None] = Phabricator.Data.Project.Selection.Selected;
            synchronizationParameters.remotelyModifiedObjects = new System.Collections.Generic.List<Phabricator.Data.PhabricatorObject>();

            Storage.Stage stageStorage = new Storage.Stage();
            foreach (string stageInfoRecord in stageInfoRecords)
            {
                Phabricator.Data.Phriction stagedPhriction = JsonConvert.DeserializeObject<Phabricator.Data.Phriction>(stageInfoRecord);
                stageStorage.Create(Database, synchronizationParameters.browser, stagedPhriction);
            }

            synchronizationController.ProgressMethod_UploadPhrictionDocuments(synchronizationParameters, 1, 2);
        }
    }
}
