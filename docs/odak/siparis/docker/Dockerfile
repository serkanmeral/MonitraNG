FROM php:7.4-apache

RUN apt-get update && apt-get install -y \
    libzip-dev \
    unzip \
    && docker-php-ext-install pdo pdo_mysql mysqli \
    && a2enmod rewrite \
    && rm -rf /var/lib/apt/lists/*

ENV APACHE_DOCUMENT_ROOT=/var/www/html/webroot
ENV CAKEPHP_UPLOAD_ROOT=/var/www/uploads/

RUN sed -ri -e 's!/var/www/html!${APACHE_DOCUMENT_ROOT}!g' /etc/apache2/sites-available/*.conf \
    && sed -ri -e 's!/var/www/!${APACHE_DOCUMENT_ROOT}!g' /etc/apache2/apache2.conf /etc/apache2/conf-available/*.conf

RUN echo 'SetEnv CAKEPHP_UPLOAD_ROOT /var/www/uploads/' >> /etc/apache2/conf-available/kalite-env.conf \
    && a2enconf kalite-env

COPY entrypoint.sh /usr/local/bin/kalite-entrypoint.sh
RUN sed -i 's/\r$//' /usr/local/bin/kalite-entrypoint.sh \
    && chmod +x /usr/local/bin/kalite-entrypoint.sh

WORKDIR /var/www/html

ENTRYPOINT ["/usr/local/bin/kalite-entrypoint.sh"]
