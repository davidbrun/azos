﻿/*<FILE_LICENSE>
 * Azos (A to Z Application Operating System) Framework
 * The A to Z Foundation (a.k.a. Azist) licenses this file to you under the MIT license.
 * See the LICENSE file in the project root for more information.
</FILE_LICENSE>*/

using System;
using System.Threading.Tasks;

using Azos.Apps.Injection;
using Azos.Data.Idgen;

namespace Azos.Data.Business
{
  /// <summary>
  /// Models a persisted entity with a unique GDID assigned on Insert by the server stack.
  /// </summary>
  /// <typeparam name="TSaveLogic">Type of IBusinessLogic which handles model persistence on save</typeparam>
  /// <typeparam name="TSaveResult">Type of SaveResult, typically a <see cref="ChangeResult"/></typeparam>
  /// <remarks>
  /// The entity models deriving this class may be used on both server and client implementations,
  /// the difference is in TSaveLogic implementation used. On the server side, the logic provides true
  /// data-store capabilities, whereas the client logic acts merely as a proxy which delegates the
  /// actual work into the server (e.g. using an HttpClient, GRPC or whatever other means);
  /// therefore, you can use the same entity in an OOP way in both client and server tiers uniformly
  /// </remarks>
  public abstract class PersistedEntity<TSaveLogic, TSaveResult> : PersistedModel<TSaveResult>
                                                                   where TSaveLogic : class, IBusinessLogic
  {
    /// <summary>
    /// Immutable primary global distributed Id (surrogate pk) for this node.
    /// You can only supply the value on update (as in using HTTP PUT), as the value is generated by server on new record (as in HTTP POST)
    /// </summary>
    [Field(required: true, key: true, Description = "Immutable primary global distributed Id (surrogate pk) for this entity. " +
    "You can only supply the value on update (as in using HTTP PUT), as the value is generated by server on new record (as in HTTP POST)")]
    public GDID Gdid { get; set; }

    /// <summary>
    /// Returns EntityId for this item
    /// </summary>
    [Field(required: true, Description = "Returns EntityId of this item")]
    public abstract EntityId Id { get; }

    [Inject(Optional = true)]
    protected IGdidProviderModule m_GdidGenerator;

    [Inject]
    protected TSaveLogic m_SaveLogic;

    /// <summary>
    /// Returns true if the target logic module is server implementation vs a client library
    /// </summary>
    protected bool IsServerImplementation => m_SaveLogic?.IsServerImplementation ?? false;

    /// <summary>
    /// Excuses GDID from required validation until later, as it is generated on server on insert only
    /// </summary>
    public override ValidState ValidateField(ValidState state, Schema.FieldDef fdef, string scope = null)
      => fdef.Name == nameof(Gdid) ? state : base.ValidateField(state, fdef, scope);


    protected override async Task<ValidState> DoAfterValidateOnSaveAsync(ValidState state)
    {
      if (IsServerImplementation)
      {
        m_GdidGenerator.NonNull("Gdid gen server capability");
      }

      state = await base.DoAfterValidateOnSaveAsync(state).ConfigureAwait(false);
      if (state.HasErrors) return state;

      if (FormMode == FormMode.Insert)
      {
        if (!Gdid.IsZero)//in insert mode the GDID must NOT be supplied
        {
          return new ValidState(state, new FieldValidationException(this, nameof(Gdid),
             "`Gdid` field value may not be provided for entity in INSERT mode as it is generated and assigned by the server. " +
             "If you are trying to re-use the same `PersistedEntity` instance (which has `Gdid` assigned by previous insert) " +
             "to insert another record/document in your store, set its `Gdid` field value to GDID.ZERO first to signify the intent"));
        }
      }
      else //non-Insert
      {
        if (Gdid.IsZero) //in non-insert mode (update), the GDID MUST be supplied
        {
          return new ValidState(state, new FieldValidationException(this, nameof(Gdid), "Gdid field value is required"));
        }
      }

      return state; //all good
    }

    protected override async Task DoBeforeSaveAsync()
    {
      await base.DoBeforeSaveAsync().ConfigureAwait(false);

      //Generate new GDID only AFTER all checks are passed not to waste gdid instance
      //in case of validation errors
      if (FormMode == FormMode.Insert && m_GdidGenerator != null)
      {
        Gdid = m_GdidGenerator.Provider.GenerateGdidFor(this.GetType());
      }
    }

    protected sealed override async Task<SaveResult<TSaveResult>> DoSaveAsync()
    {
      var logic = m_SaveLogic.NonNull("injected " + nameof(m_SaveLogic));

      var got = await SaveBody(logic).ConfigureAwait(false);

      var result = new SaveResult<TSaveResult>(got);

      return result;
    }

    /// <summary>
    /// Override to perform persistence operation triggered via call to Save().
    /// The `logic` parameter points to module which handles persistence, for example
    /// it can be injected a client logic which delegates the physical save to some other server
    /// </summary>
    /// <param name="logic">Logic to transact</param>
    /// <returns>TSaveResult of the operation</returns>
    protected abstract Task<TSaveResult> SaveBody(TSaveLogic logic);
  }
}
