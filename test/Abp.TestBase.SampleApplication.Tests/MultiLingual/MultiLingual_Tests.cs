﻿using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Abp.TestBase.SampleApplication.Shop;
using Shouldly;
using Xunit;

namespace Abp.TestBase.SampleApplication.Tests.MultiLingual
{
    public class MultiLingual_Tests: SampleApplicationTestBase
    {
        private readonly IProductAppService _productAppService;

        public MultiLingual_Tests()
        {
            _productAppService = Resolve<IProductAppService>();
        }

        [Fact]
        public async Task CreateMultiLingualMap_Tests()
        {
            CultureInfo.CurrentUICulture = new CultureInfo("tr");

            var products = await _productAppService.GetProducts();
            products.ShouldNotBeNull();

            products.Items.Count.ShouldBe(3);
            var product1 = products.Items[0];
            var product2 = products.Items[1];
            var product3 = products.Items[2];

            product1.Language.ShouldBe("tr");
            product1.Name.ShouldBe("Saat");

            product2.Language.ShouldBe("en");
            product2.Name.ShouldBe("Bike");

            product3.Language.ShouldBe("it");
            product3.Name.ShouldBe("Giornale");

            CultureInfo.CurrentUICulture = new CultureInfo("fr");

            products = await _productAppService.GetProducts();
            products.ShouldNotBeNull();

            products.Items.Count.ShouldBe(3);
            product1 = products.Items[0];
            product2 = products.Items[1];
            product3 = products.Items[2];

            product1.Language.ShouldBe("en");
            product1.Name.ShouldBe("Watch");

            product2.Language.ShouldBe("fr");
            product2.Name.ShouldBe("Bicyclette");

            product3.Language.ShouldBe("it");
            product3.Name.ShouldBe("Giornale");
        }
    }
}
