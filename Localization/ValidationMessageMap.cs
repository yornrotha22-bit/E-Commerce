namespace RothaShop.Localization;

/// <summary>
/// Data-annotation attributes and Identity carry compiled English messages, so
/// they cannot be localized where they are declared. This map pairs each known
/// English message with a dictionary key; the action filter and the controllers
/// that copy ModelState text into TempData translate through it.
/// </summary>
public static class ValidationMessageMap
{
    public static readonly IReadOnlyDictionary<string, string> EnglishToKey =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // Customer forms
            ["Enter the recipient name."] = "val.recipientName",
            ["Enter a phone number."] = "val.phoneRequired",
            ["Enter a valid phone number."] = "val.phoneInvalid",
            ["Enter the street address."] = "val.streetRequired",
            ["Enter the city or province."] = "val.cityRequired",
            ["Enter the country."] = "val.countryRequired",
            ["Enter a valid email address."] = "val.emailInvalid",
            ["Enter your email address."] = "val.emailRequired",
            ["Enter your password."] = "val.passwordRequired",
            ["Enter a password."] = "val.passwordRequired",
            ["Confirm your password."] = "val.passwordConfirm",
            ["Confirm the new password."] = "val.passwordConfirmNew",
            ["Enter a new password."] = "val.passwordNew",
            ["Enter your current password."] = "val.passwordCurrent",
            ["Use at least 8 characters."] = "val.passwordLength",
            ["The passwords do not match."] = "val.passwordMismatch",
            ["The new password and confirmation password do not match."] = "val.passwordMismatch",
            ["Enter your full name."] = "val.fullName",

            // Admin – catalog
            ["Enter the brand name."] = "val.brandName",
            ["Enter the category name."] = "val.categoryName",
            ["Enter the product name."] = "val.productName",
            ["Enter a valid absolute URL."] = "val.urlInvalid",
            ["Choose a brand."] = "val.chooseBrand",
            ["Choose a category."] = "val.chooseCategory",
            ["SKU is required."] = "val.skuRequired",
            ["Enter a SKU."] = "val.skuRequired",
            ["Enter a price greater than zero."] = "val.pricePositive",
            ["Compare-at price must be greater than zero."] = "val.compareAtPositive",
            ["Compare-at price must be at least the selling price."] = "val.compareAtTooLow",
            ["Stock cannot be negative."] = "val.stockNonNegative",
            ["Enter a slug using letters or numbers."] = "val.slugInvalid",
            ["Another brand already uses this name."] = "val.brandNameTaken",
            ["Another brand already uses this slug."] = "val.brandSlugTaken",
            ["Another category already uses this name."] = "val.categoryNameTaken",
            ["Another category already uses this slug."] = "val.categorySlugTaken",
            ["A category cannot be its own parent."] = "val.categorySelfParent",
            ["The selected parent category does not exist."] = "val.parentMissing",
            ["A category cannot be moved under one of its own sub-categories."] = "val.parentDescendant",
            ["Add at least one product variant."] = "val.variantsRequired",
            ["A variant does not belong to this product."] = "val.variantNotOwned",
            ["Choose an existing brand."] = "val.chooseExistingBrand",
            ["Choose an existing category."] = "val.chooseExistingCategory",
            ["Another product already uses this slug."] = "val.productSlugTaken",
            ["The image could not be saved."] = "val.imageSaveFailed",
            ["A gallery image could not be saved."] = "val.gallerySaveFailed",

            // Admin – promotions
            ["Enter a coupon code."] = "val.couponCodeRequired",
            ["Enter a discount value greater than zero."] = "val.discountPositive",
            ["A percentage discount cannot exceed 100."] = "val.percentMax",
            ["The end date must be after the start date."] = "val.endAfterStart",
            ["Another coupon already uses this code."] = "val.couponCodeTaken",

            // Admin – accounts
            ["Choose a valid role."] = "val.roleInvalid",
            ["An account with this email already exists."] = "val.emailTaken",
            ["The new password is not valid."] = "val.passwordNotValid",

            // Storefront
            ["Invalid email or password."] = "val.invalidLogin",
            ["This account is temporarily locked. Try again later."] = "val.accountLocked",
            ["This reset link is invalid or has expired. Request a new one."] = "val.resetLinkInvalid",
            ["This payment method is not available."] = "val.paymentUnavailable",
            ["Your cart is empty."] = "val.cartEmpty",
            ["Some cart items are unavailable. Update your cart before checking out."] = "val.cartUnavailable",
            ["The account could not be created because the customer role is unavailable. " +
             "Contact the store administrator."] = "val.customerRoleUnavailable",

            // ASP.NET Core Identity password policy
            ["Passwords must be at least 8 characters."] = "val.idMinLength",
            ["Passwords must have at least one non alphanumeric character."] = "val.idNonAlnum",
            ["Passwords must have at least one digit ('0'-'9')."] = "val.idDigit",
            ["Passwords must have at least one lowercase ('a'-'z')."] = "val.idLower",
            ["Passwords must have at least one uppercase ('A'-'Z')."] = "val.idUpper",
            ["Incorrect password."] = "val.idIncorrectPassword"
        };

    /// <summary>Returns the translated message, or the original text when it is unknown.</summary>
    public static string Translate(string? message, ITranslator translator)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        if (!EnglishToKey.TryGetValue(message, out var key))
        {
            return message;
        }

        var translated = translator[key];

        return string.IsNullOrWhiteSpace(translated) ? message : translated;
    }
}
