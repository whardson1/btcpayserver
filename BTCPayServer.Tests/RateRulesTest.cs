﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using BTCPayServer.Rating;
using Xunit;

namespace BTCPayServer.Tests
{
    public class RateRulesTest
    {
        [Fact]
        public void CanParseRateRules()
        {
            // Check happy path
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("// Some cool comments");
            builder.AppendLine("DOGE_X = DOGE_BTC * BTC_X * 1.1");
            builder.AppendLine("DOGE_BTC = Bittrex(DOGE_BTC)");
            builder.AppendLine("// Some other cool comments");
            builder.AppendLine("BTC_usd = GDax(BTC_USD)");
            builder.AppendLine("BTC_X = Coinbase(BTC_X);");
            builder.AppendLine("X_X = CoinAverage(X_X) * 1.02");

            Assert.False(RateRules.TryParse("DPW*&W&#hdi&#&3JJD", out var rules));
            Assert.True(RateRules.TryParse(builder.ToString(), out rules));
            Assert.Equal(
                "// Some cool comments\n" +
                "DOGE_X = DOGE_BTC * BTC_X * 1.1;\n" +
                "DOGE_BTC = bittrex(DOGE_BTC);\n" +
                "// Some other cool comments\n" +
                "BTC_USD = gdax(BTC_USD);\n" +
                "BTC_X = coinbase(BTC_X);\n" +
                "X_X = coinaverage(X_X) * 1.02;",
                rules.ToString());
            var tests = new[]
            {
                (Pair: "BTC_USD", Expected: "gdax(BTC_USD)"),
                (Pair: "BTC_CAD", Expected: "coinbase(BTC_CAD)"),
                (Pair: "DOGE_USD", Expected: "bittrex(DOGE_BTC) * gdax(BTC_USD) * 1.1"),
                (Pair: "DOGE_CAD", Expected: "bittrex(DOGE_BTC) * coinbase(BTC_CAD) * 1.1"),
                (Pair: "LTC_CAD", Expected: "coinaverage(LTC_CAD) * 1.02"),
            };
            foreach (var test in tests)
            {
                Assert.Equal(test.Expected, rules.GetRuleFor(CurrencyPair.Parse(test.Pair)).ToString());
            }
            rules.GlobalMultiplier = 2.32m;
            Assert.Equal("(bittrex(DOGE_BTC) * gdax(BTC_USD) * 1.1) * 2.32", rules.GetRuleFor(CurrencyPair.Parse("DOGE_USD")).ToString());
            ////////////////

            // Check errors conditions
            builder = new StringBuilder();
            builder.AppendLine("DOGE_X = LTC_CAD * BTC_X * 1.1");
            builder.AppendLine("DOGE_BTC = Bittrex(DOGE_BTC)");
            builder.AppendLine("BTC_usd = GDax(BTC_USD)");
            builder.AppendLine("LTC_CHF = LTC_CHF * 1.01");
            builder.AppendLine("BTC_X = Coinbase(BTC_X)");
            Assert.True(RateRules.TryParse(builder.ToString(), out rules));

            tests = new[]
            {
                (Pair: "LTC_CAD", Expected: "ERR_NO_RULE_MATCH(LTC_CAD)"),
                (Pair: "DOGE_USD", Expected: "ERR_NO_RULE_MATCH(LTC_CAD) * gdax(BTC_USD) * 1.1"),
                (Pair: "LTC_CHF", Expected: "ERR_TOO_MUCH_NESTED_CALLS(LTC_CHF) * 1.01"),
            };
            foreach (var test in tests)
            {
                Assert.Equal(test.Expected, rules.GetRuleFor(CurrencyPair.Parse(test.Pair)).ToString());
            }
            //////////////////

            // Check if we can resolve exchange rates
            builder = new StringBuilder();
            builder.AppendLine("DOGE_X = DOGE_BTC * BTC_X * 1.1");
            builder.AppendLine("DOGE_BTC = Bittrex(DOGE_BTC)");
            builder.AppendLine("BTC_usd = GDax(BTC_USD)");
            builder.AppendLine("BTC_X = Coinbase(BTC_X)");
            builder.AppendLine("X_X = CoinAverage(X_X) * 1.02");
            Assert.True(RateRules.TryParse(builder.ToString(), out rules));

            var tests2 = new[]
            {
                (Pair: "BTC_USD", Expected: "gdax(BTC_USD)", ExpectedExchangeRates: "gdax(BTC_USD)"),
                (Pair: "BTC_CAD", Expected: "coinbase(BTC_CAD)", ExpectedExchangeRates: "coinbase(BTC_CAD)"),
                (Pair: "DOGE_USD", Expected: "bittrex(DOGE_BTC) * gdax(BTC_USD) * 1.1", ExpectedExchangeRates: "bittrex(DOGE_BTC),gdax(BTC_USD)"),
                (Pair: "DOGE_CAD", Expected: "bittrex(DOGE_BTC) * coinbase(BTC_CAD) * 1.1", ExpectedExchangeRates: "bittrex(DOGE_BTC),coinbase(BTC_CAD)"),
                (Pair: "LTC_CAD", Expected: "coinaverage(LTC_CAD) * 1.02", ExpectedExchangeRates: "coinaverage(LTC_CAD)"),
            };
            foreach (var test in tests2)
            {
                var rule = rules.GetRuleFor(CurrencyPair.Parse(test.Pair));
                Assert.Equal(test.Expected, rule.ToString());
                Assert.Equal(test.ExpectedExchangeRates, string.Join(',', rule.ExchangeRates.OfType<object>().ToArray()));
            }
            var rule2 = rules.GetRuleFor(CurrencyPair.Parse("DOGE_CAD"));
            rule2.ExchangeRates.SetRate("bittrex", CurrencyPair.Parse("DOGE_BTC"), 5000);
            rule2.Reevaluate();
            Assert.True(rule2.HasError);
            Assert.Equal("5000 * ERR_RATE_UNAVAILABLE(coinbase, BTC_CAD) * 1.1", rule2.ToString(true));
            Assert.Equal("bittrex(DOGE_BTC) * coinbase(BTC_CAD) * 1.1", rule2.ToString(false));
            rule2.ExchangeRates.SetRate("coinbase", CurrencyPair.Parse("BTC_CAD"), 2000.4m);
            rule2.Reevaluate();
            Assert.False(rule2.HasError);
            Assert.Equal("5000 * 2000.4 * 1.1", rule2.ToString(true));
            Assert.Equal(rule2.Value, 5000m * 2000.4m * 1.1m);
            ////////

            // Make sure parenthesis are correctly calculated
            builder = new StringBuilder();
            builder.AppendLine("DOGE_X = DOGE_BTC * BTC_X");
            builder.AppendLine("BTC_USD = -3 + coinbase(BTC_CAD) + 50 - 5");
            builder.AppendLine("DOGE_BTC = 2000");
            Assert.True(RateRules.TryParse(builder.ToString(), out rules));
            rules.GlobalMultiplier = 1.1m;

            rule2 = rules.GetRuleFor(CurrencyPair.Parse("DOGE_USD"));
            Assert.Equal("(2000 * (-3 + coinbase(BTC_CAD) + 50 - 5)) * 1.1", rule2.ToString());
            rule2.ExchangeRates.SetRate("coinbase", CurrencyPair.Parse("BTC_CAD"), 1000m);
            Assert.True(rule2.Reevaluate());
            Assert.Equal("(2000 * (-3 + 1000 + 50 - 5)) * 1.1", rule2.ToString(true));
            Assert.Equal((2000m * (-3m + 1000m + 50m - 5m)) * 1.1m, rule2.Value.Value);

            // Test inverse
            rule2 = rules.GetRuleFor(CurrencyPair.Parse("USD_DOGE"));
            Assert.Equal("(1 / (2000 * (-3 + coinbase(BTC_CAD) + 50 - 5))) * 1.1", rule2.ToString());
            rule2.ExchangeRates.SetRate("coinbase", CurrencyPair.Parse("BTC_CAD"), 1000m);
            Assert.True(rule2.Reevaluate());
            Assert.Equal("(1 / (2000 * (-3 + 1000 + 50 - 5))) * 1.1", rule2.ToString(true));
            Assert.Equal(( 1.0m / (2000m * (-3m + 1000m + 50m - 5m))) * 1.1m, rule2.Value.Value);
            ////////
        }
    }
}
