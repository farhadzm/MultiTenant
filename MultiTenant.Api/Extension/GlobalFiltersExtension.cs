using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Query;
using MultiTenant.Api.Entities;
using System.Linq.Expressions;
using System.Reflection;

namespace MultiTenant.Api.Extension;

public static class GlobalFiltersExtension
{
    public static void ApplySoftDeleteQueryFilters(this ModelBuilder modelBuilder)
    {
        foreach (IMutableEntityType item in from eType in modelBuilder.Model.GetEntityTypes()
                                            where typeof(IBaseEntity).IsAssignableFrom(eType.ClrType)
                                            select eType)
        {
            item.AddSoftDeleteQueryFilter();
        }
    }

    public static void AddQueryFilter<T>(this EntityTypeBuilder<T> entityTypeBuilder, Expression<Func<T, bool>> expression) where T : class
    {
        ParameterExpression parameterExpression = Expression.Parameter(entityTypeBuilder.Metadata.ClrType);
        Expression expression2 = ReplacingExpressionVisitor.Replace(expression.Parameters.Single(), parameterExpression, expression.Body);
        LambdaExpression queryFilter = entityTypeBuilder.Metadata.GetQueryFilter();
        if (queryFilter != null)
        {
            expression2 = Expression.AndAlso(ReplacingExpressionVisitor.Replace(queryFilter.Parameters.Single(), parameterExpression, queryFilter.Body), expression2);
        }

        LambdaExpression filter = Expression.Lambda(expression2, parameterExpression);
        entityTypeBuilder.HasQueryFilter(filter);
    }

    private static void AddSoftDeleteQueryFilter(this IMutableEntityType entityData)
    {
        object obj = typeof(GlobalFiltersExtension).GetMethod("GetSoftDeleteFilter", BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(entityData.ClrType).Invoke(null, new object[0]);
        entityData.SetQueryFilter((LambdaExpression)obj);
    }

    private static LambdaExpression GetSoftDeleteFilter<TEntity>() where TEntity : IBaseEntity
    {
        return (Expression<Func<TEntity, bool>>)((TEntity entity) => !((IBaseEntity)entity).IsDeleted);
    }
}
