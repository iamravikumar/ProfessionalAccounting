﻿using System;
using System.Collections.Generic;
using System.Linq;
using AccountingServer.BLL.Parsing;
using AccountingServer.DAL;
using AccountingServer.Entities;

namespace AccountingServer.BLL
{
    /// <summary>
    ///     基本会计业务处理类
    /// </summary>
    public class Accountant
    {
        /// <summary>
        ///     数据库访问
        /// </summary>
        private readonly IDbAdapter m_Db;

        private readonly CarryAccountant m_CarryAccountant;

        private readonly AssetAccountant m_AssetAccountant;

        private readonly AmortAccountant m_AmortAccountant;

        /// <summary>
        ///     获取是否已经连接到数据库
        /// </summary>
        public bool Connected => m_Db.Connected;

        public Accountant()
        {
            var adapter = new MongoDbAdapter();

            m_Db = adapter;

            m_CarryAccountant = new CarryAccountant(m_Db);
            m_AssetAccountant = new AssetAccountant(m_Db);
            m_AmortAccountant = new AmortAccountant(m_Db);
        }

        #region Server

        public void Connect() => m_Db.Connect();

        public void Disconnect() => m_Db.Disconnect();

        #endregion

        #region Voucher

        public Voucher SelectVoucher(string id) => m_Db.SelectVoucher(id);

        public IEnumerable<Voucher> SelectVouchers(string query)
            => SelectVouchers(QueryParser.From(query).vouchers());

        public IEnumerable<Voucher> SelectVouchers(IQueryCompunded<IVoucherQueryAtom> query)
            => m_Db.SelectVouchers(query);

        public IEnumerable<Balance> SelectVoucherDetailsGrouped(string query)
            => SelectVoucherDetailsGrouped(QueryParser.From(query).groupedQuery());

        public IEnumerable<Balance> SelectVoucherDetailsGrouped(IGroupedQuery query)
        {
            var res = m_Db.SelectVoucherDetailsGrouped(query);
            if (query.Subtotal.AggrType != AggregationType.ChangedDay &&
                query.Subtotal.GatherType == GatheringType.NonZero)
                return res.Where(b => !b.Fund.IsZero());
            return res;
        }

        public bool DeleteVoucher(string id) => m_Db.DeleteVoucher(id);

        public long DeleteVouchers(string query)
            => DeleteVouchers(QueryParser.From(query).vouchers());

        public long DeleteVouchers(IQueryCompunded<IVoucherQueryAtom> query) => m_Db.DeleteVouchers(query);

        public bool Upsert(Voucher entity)
        {
            entity.Currency = entity.Currency?.ToUpperInvariant() ?? Voucher.BaseCurrency;
            return m_Db.Upsert(entity);
        }

        #endregion

        #region Asset

        public Asset SelectAsset(Guid id)
        {
            var result = m_Db.SelectAsset(id);
            AssetAccountant.InternalRegular(result);
            return result;
        }

        public IEnumerable<Asset> SelectAssets(string query)
            => SelectAssets(QueryParser.From(query).distributedQ());

        public IEnumerable<Asset> SelectAssets(IQueryCompunded<IDistributedQueryAtom> filter)
        {
            foreach (var asset in m_Db.SelectAssets(filter))
            {
                AssetAccountant.InternalRegular(asset);
                yield return asset;
            }
        }

        public bool DeleteAsset(Guid id) => m_Db.DeleteAsset(id);

        public long DeleteAssets(string query)
            => DeleteAssets(QueryParser.From(query).distributedQ());

        public long DeleteAssets(IQueryCompunded<IDistributedQueryAtom> filter) => m_Db.DeleteAssets(filter);

        public bool Upsert(Asset entity) => m_Db.Upsert(entity);

        public IEnumerable<Voucher> RegisterVouchers(Asset asset, DateFilter rng,
                                                     string query)
            => RegisterVouchers(asset, rng, QueryParser.From(query).vouchers());

        public IEnumerable<Voucher> RegisterVouchers(Asset asset, DateFilter rng,
                                                     IQueryCompunded<IVoucherQueryAtom> query)
            => m_AssetAccountant.RegisterVouchers(asset, rng, query);

        public static void Depreciate(Asset asset) => AssetAccountant.Depreciate(asset);

        public IEnumerable<AssetItem> Update(Asset asset, DateFilter rng,
                                             bool isCollapsed = false, bool editOnly = false)
            => m_AssetAccountant.Update(asset, rng, isCollapsed, editOnly);

        #endregion

        #region Amort

        public Amortization SelectAmortization(Guid id)
        {
            var result = m_Db.SelectAmortization(id);
            AmortAccountant.InternalRegular(result);
            return result;
        }

        public IEnumerable<Amortization> SelectAmortizations(string query)
            => SelectAmortizations(QueryParser.From(query).distributedQ());

        public IEnumerable<Amortization> SelectAmortizations(IQueryCompunded<IDistributedQueryAtom> filter)
        {
            foreach (var amort in m_Db.SelectAmortizations(filter))
            {
                AmortAccountant.InternalRegular(amort);
                yield return amort;
            }
        }

        public bool DeleteAmortization(Guid id) => m_Db.DeleteAmortization(id);

        public long DeleteAmortizations(string query)
            => DeleteAmortizations(QueryParser.From(query).distributedQ());

        public long DeleteAmortizations(IQueryCompunded<IDistributedQueryAtom> filter)
            => m_Db.DeleteAmortizations(filter);

        public bool Upsert(Amortization entity) => m_Db.Upsert(entity);

        public IEnumerable<Voucher> RegisterVouchers(Amortization amort, DateFilter rng,
                                                     string query)
            => RegisterVouchers(amort, rng, QueryParser.From(query).vouchers());

        public IEnumerable<Voucher> RegisterVouchers(Amortization amort, DateFilter rng,
                                                     IQueryCompunded<IVoucherQueryAtom> query)
            => m_AmortAccountant.RegisterVouchers(amort, rng, query);

        public static void Amortize(Amortization amort) => AmortAccountant.Amortize(amort);

        public IEnumerable<AmortItem> Update(Amortization amort, DateFilter rng,
                                             bool isCollapsed = false, bool editOnly = false)
            => m_AmortAccountant.Update(amort, rng, isCollapsed, editOnly);

        public static double? GetBookValueOn(IDistributed dist, DateTime? dt)
            => DistributedAccountant.GetBookValueOn(dist, dt);

        #endregion

        #region NamedQueryTemplate

        public string SelectNamedQueryTemplate(string name) => m_Db.SelectNamedQueryTemplate(name);

        public IEnumerable<KeyValuePair<string, string>> SelectNamedQueryTemplates() => m_Db.SelectNamedQueryTemplates();

        public bool DeleteNamedQueryTemplate(string name) => m_Db.DeleteNamedQueryTemplate(name);

        public bool Upsert(string name, string value) => m_Db.Upsert(name, value);

        #endregion

        #region Carry

        public void Carry(DateTime? dt) => m_CarryAccountant.Carry(dt);

        public void CarryYear(DateTime? dt, bool includeNull = false) => m_CarryAccountant.CarryYear(dt, includeNull);

        #endregion
    }
}
