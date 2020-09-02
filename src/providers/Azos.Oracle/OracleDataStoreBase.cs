/*<FILE_LICENSE>
 * Azos (A to Z Application Operating System) Framework
 * The A to Z Foundation (a.k.a. Azist) licenses this file to you under the MIT license.
 * See the LICENSE file in the project root for more information.
</FILE_LICENSE>*/

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Azos.Apps;
using Azos.Conf;
using Azos.Data.Modeling;
using Azos.Instrumentation;
using Azos.Security;
using Oracle.ManagedDataAccess.Client;


namespace Azos.Data.Access.Oracle
{
  /// <summary>
  /// Implements Oracle store base functionality
  /// </summary>
  public abstract class OracleDataStoreBase : ApplicationComponent, IDataStoreImplementation, IExternallyCallable
  {
    #region CONSTS
    public const string STR_FOR_TRUE = "T";
    public const string STR_FOR_FALSE = "F";
    #endregion

    #region .ctor/.dctor
    protected OracleDataStoreBase(IApplication app) : base(app) => ctor();
    protected OracleDataStoreBase(IApplicationComponent director) : base(director) => ctor();

    private void ctor()
    {
      m_ExternalCallHandler = new ExternalCallHandler<OracleDataStoreBase>(App, this, null,
        typeof(Instrumentation.DirectSql)
      );
    }
    #endregion

    #region Private Fields

    private string m_ConnectString;
    private string m_TargetName;
    private string m_Name;

    private NameCaseSensitivity m_CaseSensitivity = NameCaseSensitivity.ToUpper;

    private bool m_StringBool = true;

    private string m_StringForTrue = STR_FOR_TRUE;
    private string m_StringForFalse = STR_FOR_FALSE;

    private bool m_FullGDIDS = true;

    private DateTimeKind m_DateTimeKind = DateTimeKind.Utc;

    private bool m_InstrumentationEnabled;

    private ExternalCallHandler<OracleDataStoreBase> m_ExternalCallHandler;
    #endregion

    #region IInstrumentation
    [Config(Default=false)]
    [ExternalParameter(CoreConsts.EXT_PARAM_GROUP_DATA, CoreConsts.EXT_PARAM_GROUP_INSTRUMENTATION)]
    public bool InstrumentationEnabled{ get{return m_InstrumentationEnabled;} set{m_InstrumentationEnabled = value;}}

    /// <summary>
    /// Returns named parameters that can be used to control this component
    /// </summary>
    public IEnumerable<KeyValuePair<string, Type>> ExternalParameters{ get { return ExternalParameterAttribute.GetParameters(this); } }

    /// <summary>
    /// Returns named parameters that can be used to control this component
    /// </summary>
    public IEnumerable<KeyValuePair<string, Type>> ExternalParametersForGroups(params string[] groups)
    {
      return ExternalParameterAttribute.GetParameters(this, groups);
    }

    /// <summary>
    /// Gets external parameter value returning true if parameter was found
    /// </summary>
    public bool ExternalGetParameter(string name, out object value, params string[] groups)
    {
        return ExternalParameterAttribute.GetParameter(App, this, name, out value, groups);
    }

    /// <summary>
    /// Sets external parameter value returning true if parameter was found and set
    /// </summary>
    public bool ExternalSetParameter(string name, object value, params string[] groups)
    {
      return ExternalParameterAttribute.SetParameter(App, this, name, value, groups);
    }

    /// <summary>
    /// Returns a handler which processes external administration calls, such as the ones originating from
    /// the application terminal
    /// </summary>
    public virtual IExternalCallHandler GetExternalCallHandler() => m_ExternalCallHandler;
    #endregion

    #region Properties

    [Config]
    public string Name
    {
      get => m_Name.IsNotNullOrWhiteSpace() ? m_Name : GetType().FullName;
      set => m_Name = value;
    }

    public override string ComponentLogTopic => OracleConsts.ORACLE_TOPIC;

    /// <summary>
    /// Get/Sets Oracle database connection string
    /// </summary>
    [SystemAdministratorPermission(AccessLevel.ADVANCED)]
    [Config, ExternalParameter(nameof(ConnectString), ExternalParameterSecurityCheck.OnGetSet, CoreConsts.EXT_PARAM_GROUP_DATA)]
    public string ConnectString
    {
      get
      {
        return m_ConnectString ?? string.Empty;
      }
      set
      {
        m_ConnectString = value;
      }
    }

    [Config, ExternalParameter(CoreConsts.EXT_PARAM_GROUP_LOG, CoreConsts.EXT_PARAM_GROUP_DATA)]
    public StoreLogLevel DataLogLevel { get; set;}

    /// <summary>
    /// Provides schema name which is typically prepended to object names during SQL construction, e.g. "MYSCHEMA"."TABLE1"
    /// </summary>
    [Config]
    public string SchemaName { get; set; }

    [SystemAdministratorPermission(AccessLevel.ADVANCED)]
    [Config, ExternalParameter(nameof(TargetName), ExternalParameterSecurityCheck.OnSet, CoreConsts.EXT_PARAM_GROUP_DATA)]
    public virtual string TargetName
    {
      get{ return m_TargetName.IsNullOrWhiteSpace() ? "ORACLE" : m_TargetName;}
      set{ m_TargetName = value;}
    }


    /// <summary>
    /// Controls identifies case name sensitivity
    /// </summary>
    [Config]
    public NameCaseSensitivity CaseSensitivity
    {
      get => m_CaseSensitivity;
      set => m_CaseSensitivity = value;
    }

    /// <summary>
    /// When true commits boolean values as StringForTrue/StringForFalse instead of bool values. True by default
    /// </summary>
    [Config(Default=true)] public bool StringBool{ get { return m_StringBool; } set {m_StringBool = value;}}

    [Config(Default=STR_FOR_TRUE)] public string StringForTrue{ get { return m_StringForTrue; } set {m_StringForTrue = value;}}

    [Config(Default=STR_FOR_FALSE)] public string StringForFalse{ get { return m_StringForFalse; } set {m_StringForFalse = value;}}


    /// <summary>
    /// When true (default) writes gdid as byte[](era+id), false - uses ulong ID only
    /// </summary>
    [Config(Default=true)] public bool FullGDIDS{ get { return m_FullGDIDS; } set {m_FullGDIDS = value;}}

    [Config(Default=DateTimeKind.Utc)] public DateTimeKind DateTimeKind { get { return m_DateTimeKind; } set { m_DateTimeKind = value; } }
    #endregion

    #region Public
    public void TestConnection()
    {
      try
      {
        using (var cnn = GetConnection().GetAwaiter().GetResult())
        {
          var cmd = cnn.CreateCommand();
          cmd.CommandType = System.Data.CommandType.Text;
          cmd.CommandText = "SELECT 1+1 from DUAL";
          if (cmd.ExecuteScalar().ToString() != "2")
            throw new OracleDataAccessException(StringConsts.SQL_STATEMENT_FAILED_ERROR);
        }
      }
      catch (Exception error)
      {
        throw new OracleDataAccessException(string.Format(StringConsts.CONNECTION_TEST_FAILED_ERROR, error.Message), error);
      }
    }

    #endregion

    #region IConfigurable Members

    public virtual void Configure(IConfigSectionNode node)
    {
      ConfigAttribute.Apply(this, node);
    }

    #endregion

    #region Protected
    /// <summary>
    /// Converts the case of identifier based on CaseSensitivity property
    /// </summary>
    internal string AdjustObjectNameCasing(string name)
    {
      switch(CaseSensitivity)
      {
        case NameCaseSensitivity.ToLower: return name.ToLowerInvariant();
        case NameCaseSensitivity.ToUpper: return name.ToUpperInvariant();
        default: return name;
      }
    }

    /// <summary>
    /// Allocates Oracle connection. Override to do custom connection setup, such as `ALTER SESSION` etc...
    /// </summary>
    public virtual async Task<OracleConnection> GetConnection()
    {
      var connectString = this.ConnectString;

      //Try to override from the context
      var ctx = CRUDOperationCallContext.Current;
      if (ctx!=null && ctx.ConnectString.IsNotNullOrWhiteSpace())
        connectString = ctx.ConnectString;

      var cnn = new OracleConnection(connectString);

      await cnn.OpenAsync();

      return cnn;
    }
    #endregion

  }
}
