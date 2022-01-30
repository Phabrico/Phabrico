using Newtonsoft.Json;

namespace Phabrico.Plugin.Storage
{
    class PhrictionToPDFConfiguration
    {
        public static Model.PhrictionToPDFConfiguration Load(Phabrico.Storage.Database database, Phabricator.Data.Phriction phrictionDocument)
        {
            Model.PhrictionToPDFConfiguration result = null;
            string jsonConfiguration = database.GetConfigurationParameter("PhrictionToPDF::configuration");
            if (string.IsNullOrEmpty(jsonConfiguration) == false)
            {
                result = JsonConvert.DeserializeObject(jsonConfiguration, typeof(Model.PhrictionToPDFConfiguration)) as Model.PhrictionToPDFConfiguration;
            }

            if (result == null)
            {
                result = new Model.PhrictionToPDFConfiguration(phrictionDocument);
            }
            else
            {
                result.PhrictionDocument = phrictionDocument;
            }

            return result;
        }

        public static void Save(Phabrico.Storage.Database database, Model.PhrictionToPDFConfiguration configuration)
        {
            if (configuration.HeaderData != null && configuration.FooterData != null)
            {
                string jsonConfiguration = JsonConvert.SerializeObject(new
                {
                    HeaderData = new
                    {
                        configuration.HeaderData.Font,
                        configuration.HeaderData.FontSize,
                        configuration.HeaderData.Text1,
                        configuration.HeaderData.Size1,
                        configuration.HeaderData.Align1,
                        configuration.HeaderData.Text2,
                        configuration.HeaderData.Size2,
                        configuration.HeaderData.Align2,
                        configuration.HeaderData.Text3,
                        configuration.HeaderData.Align3
                    },
                    FooterData = new
                    {
                        configuration.FooterData.Font,
                        configuration.FooterData.FontSize,
                        configuration.FooterData.Text1,
                        configuration.FooterData.Align1,
                        configuration.FooterData.Size1,
                        configuration.FooterData.Text2,
                        configuration.FooterData.Align2,
                        configuration.FooterData.Size2,
                        configuration.FooterData.Text3,
                        configuration.FooterData.Align3
                    }
                });
                database.SetConfigurationParameter("PhrictionToPDF::configuration", jsonConfiguration);
            }
        }
    }
}
