# fluent-plugin-grafana-loki is not bundled in the official Fluentd image,
# so a custom build is required to ship the Loki output plugin.
FROM fluent/fluentd:v1.16-debian-1
USER root
RUN gem install fluent-plugin-grafana-loki --no-document
USER fluent
