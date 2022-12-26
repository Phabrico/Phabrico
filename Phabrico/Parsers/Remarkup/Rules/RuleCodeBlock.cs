using System.Collections.Generic;

namespace Phabrico.Parsers.Remarkup.Rules
{
    /// <summary>
    /// Abstract class holding some shared methods for RuleCodeBlockBy2WhiteSpaces and RuleCodeBlockBy3BackTicks classes.
    /// A Remarkup codeblock can be defined in 2 ways: by 2 spaces at the beginning of a line or by 3 back ticks.
    /// </summary>
    public abstract class RuleCodeBlock : RemarkupRule
    {
        /// <summary>
        /// Convert remarkup language codes to highlight.js language codes
        /// </summary>
        /// <param name="value">language code known to Phabricator</param>
        /// <returns>language code known to highlight.js library</returns>
        protected string GetHighlightJsLanguage(string value)
        {
            string result = value.ToLower().Trim();
            Dictionary<string, string> langCodes = new Dictionary<string, string>()
            {
                { "apache"       , "apache" },
                { "bash"         , "bash" },
                { "brainfuck"    , "brainfuck"},
                { "coffee"       , "coffeescript"},
                { "coffeescript" , "coffeescript"},
                { "c"            , "cpp"     },
                { "cpp"          , "cpp"     },
                { "cs"           , "cs"      },
                { "csharp"       , "cs"      },
                { "c#"           , "cs"      },
                { "css"          , "css"     },
                { "d"            , "d"       },
                { "diff"         , "diff"    },
                { "django"       , "django"  },
                { "dockerfile"   , "dockerfile"},
                { "erb"          , "erb"     },
                { "erlang"       , "erlang"  },
                { "go"           , "go"      },
                { "groovy"       , "groovy"  },
                { "haskell"      , "haskell" },
                { "html"         , "xml"     },
                { "http"         , "http"    },
                { "ini"          , "ini"     },
                { "java"         , "java"    },
                { "javascript"   , "javascript"},
                { "json"         , "json"    },
                { "makefile"     , "makefile"},
                { "markdown"     , "markdown"},
                { "nginx"        , "nginx"   },
                { "objectivec"   , "objectivec"},
                { "perl"         , "perl"    },
                { "pgsql"        , "pgsql"   },
                { "php"          , "php"     },
                { "plaintext"    , "plaintext"},
                { "powershell"   , "powershell"},
                { "properties"   , "properties"},
                { "puppet"       , "puppet"  },
                { "python"       , "python"  },
                { "ruby"         , "ruby"    },
                { "shell"        , "shell"   },
                { "sql"          , "sql"     },
                { "tsql"         , "tsql"    },
                { "tex"          , "tex"     },
                { "twig"         , "twig"    },
                { "xml"          , "xml"     },
                { "yaml"         , "yaml"    },
            };

            if (langCodes.TryGetValue(result, out result) == false)
            {
                // if the language is not known by Phabricator, show the code as 'plaintext'
                result = "plaintext";
            }

            return result;
        }
    }
}
