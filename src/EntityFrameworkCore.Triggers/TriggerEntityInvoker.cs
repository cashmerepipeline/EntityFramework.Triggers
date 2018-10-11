using System;
using System.Collections.Generic;
using System.Linq;

#if EF_CORE
using Microsoft.EntityFrameworkCore;
namespace EntityFrameworkCore.Triggers
#else
using System.Data.Entity;
namespace EntityFramework.Triggers
#endif
{
	public sealed class TriggerEntityInvoker<TDbContext, TEntity> : ITriggerEntityInvoker<TDbContext> where TDbContext : DbContext where TEntity : class {
		private static readonly Action<IInsertingEntry   <TEntity, TDbContext>, IServiceProvider> RaiseInsertingActions    = GetRaiseActions<IInsertingEntry   <TEntity, TDbContext>>(nameof(Triggers<DbContext>.GlobalInserting   ), nameof(ITriggers<DbContext>.Inserting   ));
		private static readonly Action<IUpdatingEntry    <TEntity, TDbContext>, IServiceProvider> RaiseUpdatingActions     = GetRaiseActions<IUpdatingEntry    <TEntity, TDbContext>>(nameof(Triggers<DbContext>.GlobalUpdating    ), nameof(ITriggers<DbContext>.Updating    ));
		private static readonly Action<IDeletingEntry    <TEntity, TDbContext>, IServiceProvider> RaiseDeletingActions     = GetRaiseActions<IDeletingEntry    <TEntity, TDbContext>>(nameof(Triggers<DbContext>.GlobalDeleting    ), nameof(ITriggers<DbContext>.Deleting    ));
		private static readonly Action<IInsertFailedEntry<TEntity, TDbContext>, IServiceProvider> RaiseInsertFailedActions = GetRaiseActions<IInsertFailedEntry<TEntity, TDbContext>>(nameof(Triggers<DbContext>.GlobalInsertFailed), nameof(ITriggers<DbContext>.InsertFailed));
		private static readonly Action<IUpdateFailedEntry<TEntity, TDbContext>, IServiceProvider> RaiseUpdateFailedActions = GetRaiseActions<IUpdateFailedEntry<TEntity, TDbContext>>(nameof(Triggers<DbContext>.GlobalUpdateFailed), nameof(ITriggers<DbContext>.UpdateFailed));
		private static readonly Action<IDeleteFailedEntry<TEntity, TDbContext>, IServiceProvider> RaiseDeleteFailedActions = GetRaiseActions<IDeleteFailedEntry<TEntity, TDbContext>>(nameof(Triggers<DbContext>.GlobalDeleteFailed), nameof(ITriggers<DbContext>.DeleteFailed));
		private static readonly Action<IInsertedEntry    <TEntity, TDbContext>, IServiceProvider> RaiseInsertedActions     = GetRaiseActions<IInsertedEntry    <TEntity, TDbContext>>(nameof(Triggers<DbContext>.GlobalInserted    ), nameof(ITriggers<DbContext>.Inserted    ));
		private static readonly Action<IUpdatedEntry     <TEntity, TDbContext>, IServiceProvider> RaiseUpdatedActions      = GetRaiseActions<IUpdatedEntry     <TEntity, TDbContext>>(nameof(Triggers<DbContext>.GlobalUpdated     ), nameof(ITriggers<DbContext>.Updated     ));
		private static readonly Action<IDeletedEntry     <TEntity, TDbContext>, IServiceProvider> RaiseDeletedActions      = GetRaiseActions<IDeletedEntry     <TEntity, TDbContext>>(nameof(Triggers<DbContext>.GlobalDeleted     ), nameof(ITriggers<DbContext>.Deleted     ));

		private static Action<TEntry, IServiceProvider> GetRaiseActions<TEntry>(String globalTriggersEventName, String triggersEventName)
		where TEntry : IEntry<TEntity, TDbContext>
		{
			var pairs = GetTypePairs().ToArray();
			var raiseActions = new List<Action<TEntry, IServiceProvider>>(pairs.Length);
			foreach (var (dbContextType, entityType) in pairs)
			{
				var globalTriggerEventGetter = typeof(Triggers<,>).MakeGenericType(entityType, dbContextType).GetProperty(globalTriggersEventName).GetGetMethod().CreateDelegate<Func<ITriggerEvent>>();
				var instanceTriggerEventGetter = typeof(ITriggers).GetProperty(triggersEventName).GetGetMethod().CreateDelegate<Func<ITriggers, ITriggerEvent>>();
				var triggerType = typeof(ITriggers<,>).MakeGenericType(entityType, dbContextType);

				void RaiseGlobalThenInstance(TEntry entry, IServiceProvider sp)
				{
					globalTriggerEventGetter().Raise(entry, sp);
					if (sp?.GetService(triggerType) is ITriggers triggers)
						instanceTriggerEventGetter(triggers).Raise(entry, sp);
				}

				raiseActions.Add(RaiseGlobalThenInstance);
			}
			return RaiseActions;

			void RaiseActions(TEntry entry, IServiceProvider sp)
			{
				foreach (var raiseAction in raiseActions)
					raiseAction(entry, sp);
			}

			IEnumerable<(Type dbContextType, Type entityType)> GetTypePairs()
			{
				var dbContextTypes = GetInheritanceChain<TDbContext>(typeof(DbContext));
				foreach (var entityType in GetInheritanceChain<TEntity>().Distinct())
				foreach (var dbContextType in dbContextTypes)
					yield return (dbContextType, entityType);
			}
		}

		private static List<Type> GetInheritanceChain<T>(Type terminator = null) where T : class
		{
			if (terminator == null)
				terminator = typeof(Object);
			var types = new List<Type>();
			for (var type = typeof(T);; type = type.BaseType)
			{
				types.Add(type);
				if (type == terminator)
					break;
				types.AddRange(type.GetDeclaredInterfaces().Reverse());
			}
			types.Reverse();
			return types;
		}

		public void RaiseInserting   (IServiceProvider sp, Object entity, TDbContext dbc, ref Boolean cancel)                { var entry = new InsertingEntry   <TEntity, TDbContext>((TEntity) entity, dbc, sp, cancel)     ; RaiseInsertingActions   (entry, sp); cancel = entry.Cancel; }
		public void RaiseUpdating    (IServiceProvider sp, Object entity, TDbContext dbc, ref Boolean cancel)                { var entry = new UpdatingEntry    <TEntity, TDbContext>((TEntity) entity, dbc, sp, cancel)     ; RaiseUpdatingActions    (entry, sp); cancel = entry.Cancel; }
		public void RaiseDeleting    (IServiceProvider sp, Object entity, TDbContext dbc, ref Boolean cancel)                { var entry = new DeletingEntry    <TEntity, TDbContext>((TEntity) entity, dbc, sp, cancel)     ; RaiseDeletingActions    (entry, sp); cancel = entry.Cancel; }
		public void RaiseInsertFailed(IServiceProvider sp, Object entity, TDbContext dbc, Exception ex, ref Boolean swallow) { var entry = new InsertFailedEntry<TEntity, TDbContext>((TEntity) entity, dbc, sp, ex, swallow); RaiseInsertFailedActions(entry, sp); swallow = entry.Swallow; }
		public void RaiseUpdateFailed(IServiceProvider sp, Object entity, TDbContext dbc, Exception ex, ref Boolean swallow) { var entry = new UpdateFailedEntry<TEntity, TDbContext>((TEntity) entity, dbc, sp, ex, swallow); RaiseUpdateFailedActions(entry, sp); swallow = entry.Swallow; }
		public void RaiseDeleteFailed(IServiceProvider sp, Object entity, TDbContext dbc, Exception ex, ref Boolean swallow) { var entry = new DeleteFailedEntry<TEntity, TDbContext>((TEntity) entity, dbc, sp, ex, swallow); RaiseDeleteFailedActions(entry, sp); swallow = entry.Swallow; }
		public void RaiseInserted    (IServiceProvider sp, Object entity, TDbContext dbc)                                    { var entry = new InsertedEntry    <TEntity, TDbContext>((TEntity) entity, dbc, sp)             ; RaiseInsertedActions    (entry, sp); }
		public void RaiseUpdated     (IServiceProvider sp, Object entity, TDbContext dbc)                                    { var entry = new UpdatedEntry     <TEntity, TDbContext>((TEntity) entity, dbc, sp)             ; RaiseUpdatedActions     (entry, sp); }
		public void RaiseDeleted     (IServiceProvider sp, Object entity, TDbContext dbc)                                    { var entry = new DeletedEntry     <TEntity, TDbContext>((TEntity) entity, dbc, sp)             ; RaiseDeletedActions     (entry, sp); }
	}
}