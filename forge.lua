return {
    project = {
        name = "forge",
        type = "executable",
        standard = "20",
    },
    testing = true,
    dependencies = {
        direct = {
            googletest = {
                git = "https://github.com/google/googletest.git",
                tag = "v1.14.0",
            },
        },
        conan = {},
    },
    resources = {
        files = {},
    },
    scripts = {},
    features = {
    },
    custom = {
        testing = "true",
    },
}
