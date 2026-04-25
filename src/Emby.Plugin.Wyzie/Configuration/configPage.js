define(['loading'], function (loading) {
    'use strict';

    var WyzieConfig = {
        pluginUniqueId: 'c3d9f7a0-2d4e-4b8f-9a1c-7e3d4c5a6b71'
    };

    function onViewShow() {
        var page = this;
        loading.show();
        ApiClient.getPluginConfiguration(WyzieConfig.pluginUniqueId).then(function (config) {
            page.querySelector('#ApiKey').value = config.ApiKey || '';
            page.querySelector('#IncludeHearingImpaired').checked = !!config.IncludeHearingImpaired;
            page.querySelector('#PreferredSource').value = config.PreferredSource || 'all';
            page.querySelector('#PreferredFormat').value = config.PreferredFormat || 'srt';
            page.querySelector('#MaxRetries').value = config.MaxRetries != null ? config.MaxRetries : 3;
            loading.hide();
        });
    }

    function onSubmit(e) {
        loading.show();
        var form = this;
        ApiClient.getPluginConfiguration(WyzieConfig.pluginUniqueId).then(function (config) {
            config.ApiKey = form.querySelector('#ApiKey').value.trim();
            config.IncludeHearingImpaired = form.querySelector('#IncludeHearingImpaired').checked;
            config.PreferredSource = form.querySelector('#PreferredSource').value;
            config.PreferredFormat = form.querySelector('#PreferredFormat').value;
            config.MaxRetries = parseInt(form.querySelector('#MaxRetries').value, 10) || 0;
            ApiClient.updatePluginConfiguration(WyzieConfig.pluginUniqueId, config).then(function (result) {
                Dashboard.processPluginConfigurationUpdateResult(result);
            });
        });
        e.preventDefault();
        e.stopPropagation();
        return false;
    }

    return function (view, params) {
        view.querySelector('form').addEventListener('submit', onSubmit);
        view.addEventListener('viewshow', onViewShow);
    };
});
