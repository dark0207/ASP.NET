﻿using System;
using System.Transactions;
using Abp.Domain.Repositories;
using Abp.EntityFrameworkCore.Tests.Domain;
using Abp.EntityFrameworkCore.Tests.Ef;
using Abp.Modules;
using Abp.TestBase;
using Castle.MicroKernel.Registration;
using Microsoft.EntityFrameworkCore;
using Abp.Configuration.Startup;
using Abp.Dependency;
using Abp.Reflection.Extensions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace Abp.EntityFrameworkCore.Tests
{
    [DependsOn(typeof(AbpEntityFrameworkCoreModule), typeof(AbpTestBaseModule))]
    public class EntityFrameworkCoreTestModule : AbpModule
    {
        public override void PreInitialize()
        {
            Configuration.UnitOfWork.IsolationLevel = IsolationLevel.Unspecified;

            //BloggingDbContext
            RegisterBloggingDbContextToSqliteInMemoryDb(IocManager);

            //SupportDbContext
            RegisterSupportDbContextToSqliteInMemoryDb(IocManager);

            //ShopDbContext
            RegisterShopDbContextToSqliteInMemoryDb(IocManager);

            //Custom repository
            Configuration.ReplaceService<IRepository<Post, Guid>>(() =>
            {
                IocManager.IocContainer.Register(
                    Component.For<IRepository<Post, Guid>, IPostRepository, PostRepository>()
                        .ImplementedBy<PostRepository>()
                        .LifestyleTransient()
                );
            });
        }

        public override void Initialize()
        {
            IocManager.RegisterAssemblyByConvention(typeof(EntityFrameworkCoreTestModule).GetAssembly());
        }

        private static void RegisterBloggingDbContextToSqliteInMemoryDb(IIocManager iocManager)
        {
            var builder = new DbContextOptionsBuilder<BloggingDbContext>();

            builder.ReplaceService<IEntityMaterializerSource, AbpEntityMaterializerSource>();

            var inMemorySqlite = new SqliteConnection("Data Source=:memory:");
            builder.UseSqlite(inMemorySqlite);

            iocManager.IocContainer.Register(
                Component
                    .For<DbContextOptions<BloggingDbContext>>()
                    .Instance(builder.Options)
                    .LifestyleSingleton()
            );

            inMemorySqlite.Open();
            new BloggingDbContext(builder.Options).Database.EnsureCreated();
        }

        private static void RegisterSupportDbContextToSqliteInMemoryDb(IIocManager iocManager)
        {
            var builder = new DbContextOptionsBuilder<SupportDbContext>();

            builder.ReplaceService<IEntityMaterializerSource, AbpEntityMaterializerSource>();

            var inMemorySqlite = new SqliteConnection("Data Source=:memory:");
            builder.UseSqlite(inMemorySqlite);

            iocManager.IocContainer.Register(
                Component
                    .For<DbContextOptions<SupportDbContext>>()
                    .Instance(builder.Options)
                    .LifestyleSingleton()
            );

            inMemorySqlite.Open();
            new SupportDbContext(builder.Options).Database.EnsureCreated();
        }

        private static void RegisterShopDbContextToSqliteInMemoryDb(IIocManager iocManager)
        {
            var builder = new DbContextOptionsBuilder<ShopDbContext>();

            builder.ReplaceService<IEntityMaterializerSource, AbpEntityMaterializerSource>();

            var inMemorySqlite = new SqliteConnection("Data Source=:memory:");
            builder.UseSqlite(inMemorySqlite);

            iocManager.IocContainer.Register(
                Component
                    .For<DbContextOptions<ShopDbContext>>()
                    .Instance(builder.Options)
                    .LifestyleSingleton()
            );

            inMemorySqlite.Open();
            new ShopDbContext(builder.Options).Database.EnsureCreated();
        }
    }
}