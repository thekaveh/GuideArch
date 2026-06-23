import re

from guidearch.view import theme


def test_light_tokens_match_dark_keys():
    assert set(theme.LIGHT_TOKENS) == set(theme.TOKENS), (
        "LIGHT_TOKENS and TOKENS must define the same keys"
    )


def test_all_token_values_are_hex():
    hex_re = re.compile(r"^#[0-9a-f]{6}$")
    for name, value in {**theme.TOKENS, **theme.LIGHT_TOKENS}.items():
        assert hex_re.match(value), f"{name}={value!r} is not a 6-digit lowercase hex"


def test_light_surface_differs_from_dark():
    # Guard against a copy-paste that leaves light identical to dark.
    assert theme.LIGHT_TOKENS["bg-page"] != theme.TOKENS["bg-page"]
    assert theme.LIGHT_TOKENS["text-primary"] != theme.TOKENS["text-primary"]
